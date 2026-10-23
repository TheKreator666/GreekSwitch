using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Scoring;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.Core.Session;

/// <summary>
/// The per-keystroke pipeline: buffer keys, decide at each word boundary, send corrections, and
/// undo a correction when the very next key is Backspace. Single-threaded: call it only from the
/// worker. Log events carry languages, lengths and reasons, never text.
/// </summary>
public sealed class TypingSession
{
    private readonly DecisionEngine _engine;
    private readonly KeyClassifier _classifier;
    private readonly ICorrectionOutput _output;
    private readonly INeverConvertList _neverConvert;
    private readonly ILogger _logger;
    private readonly WordBuffer _buffer = new();
    private readonly UndoTracker _undo = new();
    private volatile SessionOptions _options;

    public TypingSession(
        DecisionEngine engine,
        KeyClassifier classifier,
        ICorrectionOutput output,
        INeverConvertList neverConvert,
        ILogger logger,
        SessionOptions? options = null)
    {
        _engine = engine;
        _classifier = classifier;
        _output = output;
        _neverConvert = neverConvert;
        _logger = logger;
        _options = options ?? new SessionOptions();
    }

    public SessionOptions Options
    {
        get => _options;
        set => _options = value;
    }

    /// <summary>Number of keys in the current word (for diagnostics and tests).</summary>
    public int BufferedKeyCount => _buffer.Count;

    public void OnKey(KeyInput key, Lang activeLayout)
    {
        var classification = _classifier.Classify(key);
        if (classification.Action == KeyAction.Ignore)
        {
            return;
        }

        if (classification.Action == KeyAction.Backspace && _undo.TryTake(out var undo))
        {
            Revert(undo);
            return;
        }

        _undo.Disarm();
        switch (classification.Action)
        {
            case KeyAction.Character:
                _buffer.Append(key.ToStroke(), activeLayout);
                break;
            case KeyAction.Backspace:
                _buffer.Pop();
                break;
            case KeyAction.Delimiter:
                OnDelimiter(classification.Delimiter!);
                break;
            default:
                _buffer.Reset();
                break;
        }
    }

    /// <summary>A boundary the Windows layer saw: mouse click, focus change, caret keys and so on.</summary>
    public void Reset()
    {
        _buffer.Reset();
        _undo.Disarm();
    }

    private void OnDelimiter(Delimiter delimiter)
    {
        var correctable = delimiter.Kind is DelimiterKind.Space or DelimiterKind.Punctuation || _options.CorrectOnEnterAndTab;
        var word = _buffer.Complete(delimiter);
        if (word is null || !correctable)
        {
            return;
        }

        var decision = _engine.Decide(word);
        if (decision is not Replace replace)
        {
            _logger.LogDebug("Word kept, reason {Reason}, length {Length}", ((Keep)decision).Reason, word.Keys.Count);
            return;
        }

        if (!_output.Replace(replace.Original.Length + delimiter.Text.Length, replace.Text + delimiter.Text, replace.TargetLayout))
        {
            _logger.LogInformation("Replacement not applied, lang {From}→{To}", replace.SourceLayout.Code(), replace.TargetLayout.Code());
            return;
        }

        _undo.Arm(new UndoRecord(replace.Original, replace.Text, word.Keys, replace.SourceLayout));

        _logger.LogInformation(
            "Replacement made, lang {From}→{To}, length {Length}, reason {Reason}",
            replace.SourceLayout.Code(),
            replace.TargetLayout.Code(),
            replace.Text.Length,
            replace.Reason);
#if DEBUG
        if (_options.DebugLogText)
        {
            _logger.LogDebug("[debug text] {Original} → {Text}", replace.Original, replace.Text);
        }
#endif
    }

    /// <summary>
    /// The user's Backspace has already deleted the delimiter, so switch the layout back, delete
    /// the replacement, type the original back, and remember never to convert this word.
    /// </summary>
    private void Revert(UndoRecord undo)
    {
        if (!_output.Replace(undo.Replacement.Length, undo.Original, undo.OriginalLayout))
        {
            // The user's Backspace still deleted the delimiter; carry on as an ordinary Backspace.
            _logger.LogInformation("Undo not applied");
            _buffer.Pop();
            return;
        }

        _neverConvert.Learn(Scorer.LexicalCore(undo.Original));
        _buffer.Restore(undo.Keys, undo.OriginalLayout);

        _logger.LogInformation(
            "Replacement reverted by Backspace, lang back to {Lang}, length {Length}; added to never-convert list",
            undo.OriginalLayout.Code(),
            undo.Original.Length);
    }
}
