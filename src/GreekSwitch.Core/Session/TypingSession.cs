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
    private readonly INeverConvertList _alwaysConvert;
    private readonly ILogger _logger;
    private readonly WordBuffer _buffer = new();
    private readonly RecentRun _run = new();
    private readonly UndoTracker _undo = new();
    private volatile SessionOptions _options;
    private LastWord? _last;

    public TypingSession(
        DecisionEngine engine,
        KeyClassifier classifier,
        ICorrectionOutput output,
        INeverConvertList neverConvert,
        ILogger logger,
        SessionOptions? options = null,
        INeverConvertList? alwaysConvert = null)
    {
        _alwaysConvert = alwaysConvert ?? new NeverConvertList();
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
        if (classification.Action == KeyAction.Ignore
            || (classification.Action == KeyAction.Reset && _options.HotkeyChords.Any(c => c.Matches(key))))
        {
            return; // one of our own hotkeys: it acts on the word, so it mustn't forget it
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
                    ClearLast();
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

    /// <summary>
    /// Keys were typed that the session never saw (capture was off for a moment): the word in
    /// progress is incomplete and must not be judged, and the run no longer matches the screen.
    /// </summary>
    public void MarkPartial()
    {
        _buffer.MarkTainted();
        _run.Clear();
        ClearLast();
        _undo.Disarm();
    }

    /// <summary>
    /// Hotkey "convert last word": converts the word being typed, or else the last completed one
    /// (with its delimiter), to the other layout, even if the engine kept it. Undoable with
    /// Backspace. The opposite of the engine's choice is learned (hashed): a kept word goes on the
    /// always-convert list, a word the engine had converted goes on the never-convert list.
    /// </summary>
    /// <returns>False if there was nothing to convert or the conversion couldn't be applied.</returns>
    public bool ConvertLastWord()
    {
        KeyStroke[] keys;
        Lang typed, shown;
        string delimiter;
        var inProgress = !_buffer.IsEmpty;
        if (inProgress)
        {
            if (_buffer.IsTainted || _buffer.IsMixedLayout)
            {
                return false;
            }

            keys = [.. _buffer.Keys];
            typed = shown = _buffer.Layout;
            delimiter = "";
        }
        else if (_last is { } last)
        {
            (keys, typed, shown, delimiter) = ([.. last.Keys], last.Typed, last.Shown, last.Delimiter);
        }
        else
        {
            return false;
        }

        var target = shown.Other();
        var shownText = _engine.Display(keys, typed, shown);
        var converted = _engine.Display(keys, typed, target);
        if (!_output.Replace(shownText + delimiter, converted + delimiter, target))
        {
            _logger.LogInformation("Hotkey conversion not applied, lang {From}→{To}", shown.Code(), target.Code());
            Array.Clear(keys);
            return false;
        }

        // Learn the opposite of what the engine chose for these keys.
        var typedReading = Scorer.LexicalCore(_engine.Display(keys, typed, typed));
        var always = shown == typed;
        (always ? _alwaysConvert : _neverConvert).Learn(typedReading);
        if (!always)
        {
            _alwaysConvert.Forget(typedReading);
        }

        _run.Clear();
        _undo.Disarm();
        if (inProgress)
        {
            // The word stays on screen and may be continued in the new layout: never judge it.
            _buffer.Reset();
            _buffer.MarkTainted();
            ClearLast();
        }
        else
        {
            SetLast(keys, typed, target, delimiter);
            _undo.Arm(new UndoRecord(shownText, converted, keys, shown, Words: [], ByHotkey: true, Taught: typedReading, TaughtAlways: always));
        }

        _logger.LogInformation("Last word converted by hotkey, lang {From}→{To}, length {Length}", shown.Code(), target.Code(), converted.Length);
        Array.Clear(keys);
        return true;
    }

    /// <summary>A boundary the Windows layer saw: mouse click, focus change, caret keys and so on.</summary>
    public void Reset()
    {
        _buffer.Reset();
        _run.Clear();
        ClearLast();
        _undo.Disarm();
    }

    private void OnDelimiter(Delimiter delimiter)
    {
        var correctable = delimiter.Kind is DelimiterKind.Space or DelimiterKind.Punctuation || _options.CorrectOnEnterAndTab;
        var word = _buffer.Complete(delimiter);
        if (word is null || !correctable)
        {
            _run.Clear(); // two delimiters in a row, or Enter/Tab: the run ends
            ClearLast();  // and the last word isn't safe to retype (Enter may have sent it)
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

            if (!word.Tainted && !word.MixedLayout)
            {
                SetLast(word.Keys, word.Layout, word.Layout, delimiter.Text);
            }
            else
            {
                ClearLast();
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

        if (!_output.Replace(original + delimiter.Text, replacement + delimiter.Text, replace.TargetLayout))
        {
            _logger.LogInformation("Replacement not applied, lang {From}→{To}", replace.SourceLayout.Code(), replace.TargetLayout.Code());
            _run.Clear();
            SetLast(word.Keys, word.Layout, word.Layout, delimiter.Text);
            return;
        }

        if (replace.TurnOffCapsLock)
        {
            _output.TurnOffCapsLock();
        }

        for (var i = first; i < _run.Words.Count; i++)
        {
            _run.MarkShown(i, replace.TargetLayout);
        }

        _run.Add(word.Keys, word.Layout, replace.TargetLayout);
        SetLast(word.Keys, word.Layout, replace.TargetLayout, delimiter.Text);
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
        ClearLast();
        if (!_output.Replace(undo.Replacement, undo.Original, undo.OriginalLayout))
        {
            // The user's Backspace still deleted the delimiter; carry on as an ordinary Backspace.
            _logger.LogInformation("Undo not applied");
            _buffer.Pop();
            return;
        }

        if (undo.ByHotkey && undo.Taught is { } taught)
        {
            (undo.TaughtAlways ? _alwaysConvert : _neverConvert).Forget(taught); // the user took it back
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

    private void SetLast(IReadOnlyList<KeyStroke> keys, Lang typed, Lang shown, string delimiter)
    {
        ClearLast();
        _last = new LastWord([.. keys], typed, shown, delimiter);
    }

    private void ClearLast()
    {
        if (_last is { } last)
        {
            Array.Clear(last.Keys);
            _last = null;
        }
    }

    /// <summary>The last completed word: its keys, the layout they were typed in, what it shows now, and its delimiter.</summary>
    private sealed record LastWord(KeyStroke[] Keys, Lang Typed, Lang Shown, string Delimiter);
}
