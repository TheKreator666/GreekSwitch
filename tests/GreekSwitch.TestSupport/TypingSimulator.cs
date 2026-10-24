using System.Text;
using GreekSwitch.Core;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Loanwords;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Session;

namespace GreekSwitch.TestSupport;

/// <summary>
/// A text box plus a typist. Keys reach the "screen" first, decoded with the active layout,
/// and then the session, as in the real app where the hook passes keys straight through.
/// Corrections from the session edit the screen and may switch the active layout.
/// </summary>
public sealed class TypingSimulator : ICorrectionOutput, ISuggestionSink
{
    /// <summary>Characters the simulator types as word boundaries (the classifier's delimiters plus whitespace).</summary>
    public const string DelimiterChars = " .,!?()[]{}\"-\n\t";

    private readonly StringBuilder _committed = new();
    private readonly List<KeyStroke> _pending = [];
    private readonly DualDecoder _decoder = new(StaticLayouts.Greek, StaticLayouts.English);
    private readonly bool _followLayoutSwitches;
    private bool _started;

    /// <param name="data">Real dictionaries and trigram models.</param>
    /// <param name="followLayoutSwitches">
    /// True: once the session switches the layout, later keys appear in the new layout (the real
    /// behaviour). False: every word appears exactly as written in the input, i.e. the user kept
    /// typing in whichever layout each word shows.
    /// </param>
    public TypingSimulator(
        ILanguageData data,
        bool followLayoutSwitches = true,
        ScoringOptions? scoring = null,
        SessionOptions? session = null,
        IEnumerable<string>? neverConvert = null,
        ILoanwordDetector? loanwords = null)
    {
        _followLayoutSwitches = followLayoutSwitches;
        NeverConvert = new NeverConvertList(neverConvert);
        AlwaysConvert = new NeverConvertList();
        Logger = new CapturingLogger();
        Engine = EngineFactory.Create(data, NeverConvert, scoring, AlwaysConvert);
        Session = new TypingSession(
            Engine,
            new KeyClassifier(StaticLayouts.Greek, StaticLayouts.English),
            this,
            NeverConvert,
            Logger,
            session,
            AlwaysConvert,
            loanwords,
            this);
    }

    /// <summary>The suggestion on show (as the popup would show it), or null.</summary>
    public string? Suggestion { get; private set; }

    /// <summary>Every suggestion shown so far.</summary>
    public List<string> SuggestionsShown { get; } = [];

    void ISuggestionSink.Show(string text)
    {
        Suggestion = text;
        SuggestionsShown.Add(text);
    }

    void ISuggestionSink.Hide() => Suggestion = null;

    /// <summary>Tab as the real hook handles it: swallowed to accept a visible suggestion, otherwise typed.</summary>
    public TypingSimulator Tab()
    {
        if (Suggestion is null)
        {
            return Press(new KeyInput(VirtualKeys.Tab));
        }

        Suggestion = null;
        if (!Session.AcceptSuggestion())
        {
            Press(new KeyInput(VirtualKeys.Tab)); // the worker passes the Tab on
        }

        return this;
    }

    public TypingSession Session { get; }

    public DecisionEngine Engine { get; }

    public NeverConvertList NeverConvert { get; }

    /// <summary>Words the user converted by hotkey (learned, hashed).</summary>
    public NeverConvertList AlwaysConvert { get; }

    public bool CapsLockTurnedOff { get; private set; }

    public CapturingLogger Logger { get; }

    public Lang ActiveLayout { get; private set; }

    public List<Lang> LayoutSwitches { get; } = [];

    public int Replacements { get; private set; }

    public string Screen => _committed + _decoder.Decode(_pending, ActiveLayout);

    /// <summary>
    /// Types <paramref name="text"/> as it would look with no corrector running: each word in
    /// the layout its script implies (Greek letters → Greek layout, otherwise English).
    /// </summary>
    public TypingSimulator Type(string text)
    {
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (DelimiterChars.Contains(c, StringComparison.Ordinal))
            {
                TypeWord(word.ToString());
                word.Clear();
                Press(DelimiterKey(c));
            }
            else
            {
                word.Append(c);
            }
        }

        TypeWord(word.ToString());
        return this;
    }

    /// <summary>Types the keys of <paramref name="text"/> on <paramref name="layout"/> with that layout active.</summary>
    public TypingSimulator TypeIn(Lang layout, string text, bool capsLock = false)
    {
        ActiveLayout = layout;
        _started = true;
        var keys = capsLock
            ? KeyPlanner.Plan(text, StaticLayouts.For(layout), capsLock: true) ?? throw new ArgumentException("Can't type that with Caps Lock on.", nameof(text))
            : Typist.Type(text, layout);
        foreach (var key in keys)
        {
            Press(new KeyInput(key.VirtualKey, key.Shift, key.CapsLock));
        }

        return this;
    }

    public void TurnOffCapsLock() => CapsLockTurnedOff = true;

    /// <summary>
    /// Types the keys a <paramref name="intended"/>-language typist presses for <paramref name="text"/>,
    /// whatever layout is active: the situation where the user forgot to switch. Returns each
    /// word with whether it was typed while the wrong layout was active.
    /// </summary>
    public IReadOnlyList<(string Word, bool WrongLayout)> TypeIntended(string text, Lang intended)
    {
        var words = new List<(string, bool)>();
        var word = new StringBuilder();

        void Flush()
        {
            if (word.Length == 0)
            {
                return;
            }

            words.Add((word.ToString(), ActiveLayout != intended));
            foreach (var key in Typist.Type(word.ToString(), intended))
            {
                Press(new KeyInput(key.VirtualKey, key.Shift, key.CapsLock));
            }

            word.Clear();
        }

        foreach (var c in text)
        {
            if (DelimiterChars.Contains(c, StringComparison.Ordinal))
            {
                Flush();
                Press(DelimiterKey(c));
            }
            else
            {
                word.Append(c);
            }
        }

        Flush();
        return words;
    }

    /// <summary>The user's layout is (wrongly) <paramref name="layout"/> from now on.</summary>
    public TypingSimulator SetActiveLayout(Lang layout)
    {
        ActiveLayout = layout;
        _started = true;
        return this;
    }

    /// <summary>The user presses the layout hotkey (Alt+Shift), possibly in the middle of a word.</summary>
    public TypingSimulator SwitchLayoutWithHotkey()
    {
        // What is already on screen stays as it was typed.
        _committed.Append(_decoder.Decode(_pending, ActiveLayout));
        _pending.Clear();
        Press(new KeyInput(VirtualKeys.LMenu, Alt: true));
        Press(new KeyInput(VirtualKeys.LShift, Shift: true, Alt: true));
        ActiveLayout = ActiveLayout.Other();
        return this;
    }

    public TypingSimulator Backspace() => Press(new KeyInput(VirtualKeys.Back));

    public TypingSimulator Press(KeyInput key)
    {
        ApplyToScreen(key);
        Session.OnKey(key, ActiveLayout);
        return this;
    }

    /// <summary>Make every Replace fail, as the Corrector does when a guard trips.</summary>
    public bool RefuseReplacements { get; set; }

    public bool Replace(string before, string after, Lang layout)
    {
        if (RefuseReplacements)
        {
            return false;
        }

        _committed.Append(_decoder.Decode(_pending, ActiveLayout));
        _pending.Clear();

        // The session must only ever replace exactly what is on screen.
        if (!_committed.ToString().EndsWith(before, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Replace expected \"{before}\" before the caret, but the screen ends with \"{_committed}\".");
        }

        var backspaces = before.Length;
        var text = after;

        LayoutSwitches.Add(layout);
        if (_followLayoutSwitches)
        {
            ActiveLayout = layout;
        }

        _committed.Remove(_committed.Length - backspaces, backspaces).Append(text);
        Replacements++;
        return true;
    }

    private static KeyInput DelimiterKey(char c) => c switch
    {
        ' ' => new KeyInput(VirtualKeys.Space),
        '\n' => new KeyInput(VirtualKeys.Return),
        '\t' => new KeyInput(VirtualKeys.Tab),
        _ => Typist.Type(c.ToString(), Lang.English).Select(k => new KeyInput(k.VirtualKey, k.Shift)).Single(),
    };

    private void TypeWord(string word)
    {
        if (word.Length == 0)
        {
            return;
        }

        var layout = word.Any(char.IsLetter) ? Typist.InferLayout(word) : ActiveLayout;
        if (!_followLayoutSwitches || !_started)
        {
            ActiveLayout = layout;
            _started = true;
        }

        foreach (var key in Typist.Type(word, layout))
        {
            Press(new KeyInput(key.VirtualKey, key.Shift, key.CapsLock));
        }
    }

    private void ApplyToScreen(KeyInput key)
    {
        // Modifiers alone, and shortcuts (Ctrl/Alt/Win combos, including our own hotkeys, which
        // Windows consumes), don't type anything.
        if (VirtualKeys.IsModifier(key.VirtualKey) || key.Control || key.Alt || key.Win)
        {
            return;
        }

        switch (key.VirtualKey)
        {
            case VirtualKeys.Back when _pending.Count > 0:
                _pending.RemoveAt(_pending.Count - 1);
                return;
            case VirtualKeys.Back:
                if (_committed.Length > 0)
                {
                    _committed.Length--;
                }

                return;
        }

        var delimiterText = key.VirtualKey switch
        {
            VirtualKeys.Space => " ",
            VirtualKeys.Return => "\n",
            VirtualKeys.Tab => "\t",
            VirtualKeys.Oem1 => null,
            _ => StaticLayouts.English.TryMap(key.ToStroke(), out var en) && DelimiterChars.Contains(en.Character, StringComparison.Ordinal)
                ? en.Character.ToString()
                : null,
        };

        if (delimiterText is null)
        {
            _pending.Add(key.ToStroke());
            return;
        }

        _committed.Append(_decoder.Decode(_pending, ActiveLayout)).Append(delimiterText);
        _pending.Clear();
    }
}
