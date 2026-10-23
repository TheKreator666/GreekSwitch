using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Scoring;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.Core.Session;

/// <summary>
/// The per-keystroke pipeline: buffer keys, decide at each word boundary, send corrections, and
/// undo a correction when the very next key is Backspace. After a confident correction, the
/// last few words of the run are judged again with the new context and fixed in the same batch.
/// Single-threaded: call it only from the worker. Log events carry languages, lengths, counts
/// and reasons, never text.
/// </summary>
public sealed class TypingSession
{
    private readonly DecisionEngine _engine;
    private readonly KeyClassifier _classifier;
    private readonly ICorrectionOutput _output;
    private readonly INeverConvertList _neverConvert;
    private readonly ILogger _logger;
    private readonly WordBuffer _buffer = new();
    private readonly RecentRun _run = new();
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
                if (!_buffer.Pop())
                {
                    _run.Clear(); // deleted into text before the word: the run no longer matches the screen
                }

                break;
            case KeyAction.Delimiter:
                OnDelimiter(classification.Delimiter!);
                break;
            default:
                Reset();
                break;
        }
    }

    /// <summary>A boundary the Windows layer saw: mouse click, focus change, caret keys and so on.</summary>
    public void Reset()
    {
        _buffer.Reset();
        _run.Clear();
        _undo.Disarm();
    }

    private void OnDelimiter(Delimiter delimiter)
    {
        var correctable = delimiter.Kind is DelimiterKind.Space or DelimiterKind.Punctuation || _options.CorrectOnEnterAndTab;
        var word = _buffer.Complete(delimiter);
        if (word is null || !correctable)
        {
            _run.Clear(); // two delimiters in a row, or Enter/Tab: the run ends
            return;
        }

        var decision = _engine.Decide(word);
        if (decision is Replace replace)
        {
            ApplyReplacement(word, replace, delimiter);
        }
        else
        {
            _logger.LogDebug("Word kept, reason {Reason}, length {Length}", ((Keep)decision).Reason, word.Keys.Count);
            var reconsiderable = !word.Tainted && !word.MixedLayout && delimiter.Kind == DelimiterKind.Space;
            if (reconsiderable)
            {
                _run.Add(word.Keys, word.Layout, word.Layout);
            }
            else
            {
                _run.Clear();
            }
        }

        if (delimiter.Kind != DelimiterKind.Space)
        {
            _run.Clear(); // punctuation ends the run
        }
    }

    private void ApplyReplacement(CompletedWord word, Replace replace, Delimiter delimiter)
    {
        // Earlier words of the run that now convert too (newest first, contiguous).
        var earlier = DecisionEngine.IsConfident(replace.Reason) ? Reconsider(replace.TargetLayout) : [];

        var originals = new List<string>();
        var replacements = new List<string>();
        var first = _run.Words.Count - earlier.Count;
        for (var i = first; i < _run.Words.Count; i++)
        {
            var w = _run.Words[i];
            originals.Add(_engine.Display(w.Keys, w.TypedLayout, w.Shown));
            replacements.Add(earlier[i - first]);
        }

        originals.Add(replace.Original);
        replacements.Add(replace.Text);
        var original = string.Join(' ', originals);
        var replacement = string.Join(' ', replacements);

        if (!_output.Replace(original.Length + delimiter.Text.Length, replacement + delimiter.Text, replace.TargetLayout))
        {
            _logger.LogInformation("Replacement not applied, lang {From}→{To}", replace.SourceLayout.Code(), replace.TargetLayout.Code());
            _run.Clear();
            return;
        }

        for (var i = first; i < _run.Words.Count; i++)
        {
            _run.MarkShown(i, replace.TargetLayout);
        }

        _run.Add(word.Keys, word.Layout, replace.TargetLayout);
        _undo.Arm(new UndoRecord(
            original,
            replacement,
            word.Keys,
            replace.SourceLayout,
            earlier.Count > 0 ? [.. originals.Select(Scorer.LexicalCore)] : null));

        _logger.LogInformation(
            "Replacement made, lang {From}→{To}, length {Length}, reason {Reason}, earlier words fixed {Earlier}",
            replace.SourceLayout.Code(),
            replace.TargetLayout.Code(),
            replace.Text.Length,
            replace.Reason,
            earlier.Count);
#if DEBUG
        if (_options.DebugLogText)
        {
            _logger.LogDebug("[debug text] {Original} → {Text}", original, replacement);
        }
#endif
    }

    /// <summary>
    /// Judges the kept words at the end of the run again, as if the context were
    /// <paramref name="target"/>; returns the new texts of the trailing words that now convert.
    /// </summary>
    private List<string> Reconsider(Lang target)
    {
        var texts = new List<string>();
        for (var i = _run.Words.Count - 1; i >= 0; i--)
        {
            var w = _run.Words[i];
            if (w.Shown != w.TypedLayout || w.TypedLayout == target)
            {
                break; // already converted, or already in the target language
            }

            if (_engine.Reconsider(w.Keys, w.TypedLayout, target) is not Replace again || again.TargetLayout != target)
            {
                break;
            }

            texts.Insert(0, again.Text);
        }

        return texts;
    }

    /// <summary>
    /// The user's Backspace has already deleted the delimiter, so switch the layout back, delete
    /// the replacement, type the original back, and remember never to convert the words.
    /// </summary>
    private void Revert(UndoRecord undo)
    {
        _run.Clear();
        if (!_output.Replace(undo.Replacement.Length, undo.Original, undo.OriginalLayout))
        {
            // The user's Backspace still deleted the delimiter; carry on as an ordinary Backspace.
            _logger.LogInformation("Undo not applied");
            _buffer.Pop();
            return;
        }

        foreach (var word in undo.Words ?? [Scorer.LexicalCore(undo.Original)])
        {
            _neverConvert.Learn(word);
        }

        _buffer.Restore(undo.Keys, undo.OriginalLayout);

        _logger.LogInformation(
            "Replacement reverted by Backspace, lang back to {Lang}, length {Length}, words {Words}; added to never-convert list",
            undo.OriginalLayout.Code(),
            undo.Original.Length,
            undo.Words?.Count ?? 1);
    }
}
