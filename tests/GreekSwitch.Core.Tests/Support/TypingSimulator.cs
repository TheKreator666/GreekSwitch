using System.Text;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Session;

namespace GreekSwitch.Core.Tests.Support;

/// <summary>
/// A text box plus a typist. Keys reach the "screen" first, decoded with the active layout,
/// and then the session, as in the real app where the hook passes keys straight through.
/// Corrections from the session edit the screen and may switch the active layout.
/// </summary>
public sealed class TypingSimulator : ICorrectionOutput
{
    private const string DelimiterChars = " .,!?()[]{}\"-\n\t";

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
        LanguageDataFixture data,
        bool followLayoutSwitches = true,
        ScoringOptions? scoring = null,
        SessionOptions? session = null,
        IEnumerable<string>? neverConvert = null)
    {
        _followLayoutSwitches = followLayoutSwitches;
        NeverConvert = new NeverConvertList(neverConvert);
        Logger = new CapturingLogger();
        var decoder = new DualDecoder(StaticLayouts.Greek, StaticLayouts.English);
        Engine = new DecisionEngine(decoder, new Scorer(data.Dictionaries, data.Trigrams), NeverConvert, scoring);
        Session = new TypingSession(
            Engine,
            new KeyClassifier(StaticLayouts.Greek, StaticLayouts.English),
            this,
            NeverConvert,
            Logger,
            session);
    }

    public TypingSession Session { get; }

    public DecisionEngine Engine { get; }

    public NeverConvertList NeverConvert { get; }

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
    public TypingSimulator TypeIn(Lang layout, string text)
    {
        ActiveLayout = layout;
        _started = true;
        foreach (var key in Typist.Type(text, layout))
        {
            Press(new KeyInput(key.VirtualKey, key.Shift, key.CapsLock));
        }

        return this;
    }

    public TypingSimulator Backspace() => Press(new KeyInput(VirtualKeys.Back));

    public TypingSimulator Press(KeyInput key)
    {
        ApplyToScreen(key);
        Session.OnKey(key, ActiveLayout);
        return this;
    }

    public bool ReplaceText(int backspaces, string text)
    {
        _committed.Append(_decoder.Decode(_pending, ActiveLayout));
        _pending.Clear();
        _committed.Remove(_committed.Length - backspaces, backspaces).Append(text);
        Replacements++;
        return true;
    }

    public void SwitchLayout(Lang layout)
    {
        LayoutSwitches.Add(layout);
        if (_followLayoutSwitches)
        {
            ActiveLayout = layout;
        }
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
