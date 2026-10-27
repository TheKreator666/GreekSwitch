using GreekSwitch.App.Policy;
using GreekSwitch.App.Settings;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Loanwords;

namespace GreekSwitch.App.UI;

/// <summary>The settings dialog, built in code. Edits a copy; the caller applies it on OK.</summary>
internal sealed class SettingsForm : Form
{
    /// <summary>Shown next to every control the IT policy locks.</summary>
    public const string SetByOrganization = "Set by your organization";

    private readonly ManagedPolicy _managed;
    private readonly CheckBox _enabled = new() { Text = "&Enabled", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with &Windows", AutoSize = true };
    private readonly CheckBox _correctOnEnter = new() { Text = "Also correct words ended by Enter or &Tab", AutoSize = true };
    private readonly CheckBox _flash = new() { Text = "&Flash the tray icon when a word is corrected", AutoSize = true };
    private readonly CheckBox _capsLock = new() { Text = "Fix words typed with &Caps Lock on by accident (hELLO → Hello)", AutoSize = true };
    private readonly CheckBox _accents = new() { Text = "Add missing Greek &accents (ειναι → είναι)", AutoSize = true };
    private readonly Dictionary<HotkeyAction, TextBox> _hotkeys = Enum.GetValues<HotkeyAction>().ToDictionary(a => a, _ => new TextBox { Width = 200 });
    private readonly NumericUpDown _minLength = new() { Minimum = 1, Maximum = 20, Width = 70 };
    private readonly NumericUpDown _threshold = new() { Minimum = 0, Maximum = 20, DecimalPlaces = 1, Increment = 0.5m, Width = 70 };
    private readonly CheckBox _suggestLoanwords = new() { Text = "Suggest English words written in &Greek letters (κοντέινερ → container; Tab accepts)", AutoSize = true };
    private readonly CheckBox _autoLoanwords = new() { Text = "…and replace clear tech terms automatically (ντεπλόι → deploy)", AutoSize = true };
    private readonly TextBox _excluded = MultilineBox();
    private readonly TextBox _suggestOnly = MultilineBox(60);
    private readonly TextBox _neverConvert = MultilineBox();
    private readonly TextBox _loanwordPairs = MultilineBox(60);

    /// <param name="managed">IT policy: the controls it sets are shown disabled, with their policy values.</param>
    /// <param name="learnedCount">Words learned from undo and from the convert hotkey (both hashed).</param>
    /// <param name="unavailableHotkeys">Hotkeys another program already owns.</param>
    public SettingsForm(AppSettings settings, ManagedPolicy managed, bool startWithWindows, int learnedCount, IReadOnlyList<string> unavailableHotkeys)
    {
        _managed = managed;
        Text = "GreekSwitch Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Font = SystemFonts.MessageBoxFont ?? Font;

        _enabled.Checked = managed.EffectiveEnabled(settings);
        if (managed.Enabled is not null)
        {
            _enabled.Enabled = false;
            _enabled.Text += $" ({SetByOrganization})";
        }

        _suggestLoanwords.Checked = managed.EffectiveSuggestLoanwords(settings);
        if (managed.DisableLoanwordSuggestions)
        {
            _suggestLoanwords.Enabled = false;
            _suggestLoanwords.Text += $" ({SetByOrganization})";
        }

        _autoLoanwords.Checked = settings.AutoReplaceLoanwords;
        _suggestOnly.Text = string.Join(Environment.NewLine, settings.SuggestOnlyProcesses);
        _loanwordPairs.Text = string.Join(Environment.NewLine, settings.LoanwordPairs.Select(p => $"{p.Greek} = {p.Target}"));
        _startWithWindows.Checked = startWithWindows;
        _correctOnEnter.Checked = settings.CorrectOnEnterAndTab;
        _flash.Checked = settings.FlashOnCorrection;
        _capsLock.Checked = settings.FixAccidentalCapsLock;
        _accents.Checked = settings.RestoreAccents;
        foreach (var (action, box) in _hotkeys)
        {
            box.Text = AppHotkeys.Text(settings, action);
        }

        _minLength.Value = settings.MinLength;
        _threshold.Value = (decimal)settings.ReplaceThreshold;
        _excluded.Text = string.Join(Environment.NewLine, managed.EffectiveExcluded(settings));
        _excluded.ReadOnly = managed.ExcludedProcesses is not null;
        _neverConvert.Text = string.Join(Environment.NewLine, settings.NeverConvert);
        Result = settings;

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var restore = new Button { Text = "Restore default exclusions", AutoSize = true, Enabled = managed.ExcludedProcesses is null };
        restore.Click += (_, _) => _excluded.Text = string.Join(Environment.NewLine, AppSettings.DefaultExcludedProcesses);
        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, _) =>
        {
            if ((HotkeyError() ?? PairsError()) is { } error)
            {
                MessageBox.Show(this, error, "GreekSwitch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; // keep the dialog open
                return;
            }

            Result = Collect(settings);
        };

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddRow(layout, _enabled);
        AddRow(layout, _startWithWindows);
        AddRow(layout, _correctOnEnter);
        AddRow(layout, _flash);
        AddRow(layout, _capsLock);
        AddRow(layout, _accents);
        AddRow(layout, Label("Minimum word length (keys):"), _minLength);
        AddRow(layout, Label("Replace threshold (score margin):"), _threshold);
        foreach (var (action, box) in _hotkeys)
        {
            AddRow(layout, Label($"{AppHotkeys.Name(action)} hotkey:"), box);
        }

        AddRow(layout, Label("Hotkeys: e.g. Ctrl+Alt+Space, Win+Shift+K, F9. Leave empty to turn one off."));
        if (unavailableHotkeys.Count > 0)
        {
            AddRow(layout, Label("Already used by another program, choose other keys: " + string.Join(", ", unavailableHotkeys) + "."));
        }

        AddRow(layout, Label(managed.ExcludedProcesses is null ? "Excluded apps (one .exe per line):" : $"Excluded apps ({SetByOrganization}):"));
        AddRow(layout, _excluded);
        AddRow(layout, restore);
        AddRow(layout, Label("Only suggest corrections, never make them, in these apps (one .exe per line):"));
        AddRow(layout, _suggestOnly);
        AddRow(layout, Label("Never convert these words (one per line):"));
        AddRow(layout, _neverConvert);
        AddRow(layout, _suggestLoanwords);
        AddRow(layout, _autoLoanwords);
        AddRow(layout, Label("Always replace these words (one \"Greek = English\" per line):"));
        AddRow(layout, _loanwordPairs);
        var learned = Label(managed.DisableUndoLearning
            ? $"Learning from undo, the convert hotkey and suggestions is off ({SetByOrganization}). {learnedCount} word(s) learned earlier still apply, stored only as unreadable hashes."
            : $"Plus {learnedCount} word(s) learned from undo, the convert hotkey and suggestions, stored only as unreadable hashes.");
        var forget = new Button { Text = "Forget learned words", AutoSize = true, Enabled = learnedCount > 0 };
        forget.Click += (_, _) =>
        {
            ForgetLearnedWords = true;
            forget.Enabled = false;
            learned.Text = "Learned words will be forgotten when you press OK.";
        };
        AddRow(layout, learned, forget);
        AddRow(layout, Label(managed.AdditionalAlwaysExcluded.Count == 0
            ? "Password managers are always excluded."
            : $"Password managers and {managed.AdditionalAlwaysExcluded.Count} more app(s) are always excluded ({SetByOrganization}): {string.Join(", ", managed.AdditionalAlwaysExcluded)}."));
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        AddRow(layout, buttons);
        Controls.Add(layout);
    }

    public AppSettings Result { get; private set; }

    public bool StartWithWindows => _startWithWindows.Checked;

    /// <summary>The user asked to forget every learned word (from undo and from the convert hotkey).</summary>
    public bool ForgetLearnedWords { get; private set; }

    private static TextBox MultilineBox(int height = 110) => new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Width = 420,
        Height = height,
        AcceptsReturn = true,
    };

    /// <summary>"κοντέινερ = container" per line, or null for a line that isn't a pair.</summary>
    private static LoanwordPair? ParsePair(string line)
    {
        var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts[0].Length > 0 && LoanwordPairStore.IsValidLearnedTarget(parts[1])
            ? new LoanwordPair(parts[0], parts[1])
            : null;
    }

    private string? PairsError() =>
        Lines(_loanwordPairs).FirstOrDefault(l => ParsePair(l) is null) is { } bad
            ? $"\"{bad}\" is not a pair. Write the Greek word, then =, then one English word, e.g. κοντέινερ = container."
            : null;

    private static Label Label(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };

    private static void AddRow(TableLayoutPanel layout, Control control)
    {
        layout.Controls.Add(control);
        layout.SetColumnSpan(control, 2);
    }

    private static void AddRow(TableLayoutPanel layout, Control left, Control right)
    {
        layout.Controls.Add(left);
        layout.Controls.Add(right);
    }

    private static List<string> Lines(TextBox box) =>
        [.. box.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Each hotkey must be empty or one key plus modifiers, and no two may be the same.</summary>
    private string? HotkeyError()
    {
        var seen = new HashSet<KeyChord>();
        foreach (var (action, box) in _hotkeys)
        {
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                continue;
            }

            if (HotkeyText.Parse(box.Text) is not { } chord)
            {
                return $"\"{box.Text.Trim()}\" is not a hotkey ({AppHotkeys.Name(action)}). Use one key plus Ctrl, Alt, Shift or Win, e.g. Ctrl+Alt+Space.";
            }

            if (!chord.Control && !chord.Alt && !chord.Win && chord.VirtualKey is not (>= 0x70 and <= 0x87 or 0x13 or 0x91))
            {
                return $"{AppHotkeys.Name(action)}: add Ctrl, Alt or Win, or that key could no longer be typed.";
            }

            if (!seen.Add(chord))
            {
                return $"{AppHotkeys.Name(action)}: the same hotkey is used twice.";
            }
        }

        return null;
    }

    private string HotkeyValue(HotkeyAction action) =>
        HotkeyText.Parse(_hotkeys[action].Text) is { } chord ? HotkeyText.Format(chord) : "";

    private AppSettings Collect(AppSettings original) => new()
    {
        // Values set by policy keep the user's own choice underneath, for when the policy goes away.
        Enabled = _managed.Enabled is null ? _enabled.Checked : original.Enabled,
        CorrectOnEnterAndTab = _correctOnEnter.Checked,
        FlashOnCorrection = _flash.Checked,
        FixAccidentalCapsLock = _capsLock.Checked,
        RestoreAccents = _accents.Checked,
        ConvertLastWordHotkey = HotkeyValue(HotkeyAction.ConvertLastWord),
        ConvertSelectionHotkey = HotkeyValue(HotkeyAction.ConvertSelection),
        ToggleHotkey = HotkeyValue(HotkeyAction.Toggle),
        FirstRunCompleted = original.FirstRunCompleted,
        // Unchanged underneath while the policy turns suggestions off.
        SuggestLoanwords = _managed.DisableLoanwordSuggestions ? original.SuggestLoanwords : _suggestLoanwords.Checked,
        AutoReplaceLoanwords = _autoLoanwords.Checked,
        LoanwordPairs = [.. Lines(_loanwordPairs).Select(ParsePair).OfType<LoanwordPair>()],
        LoanwordLearned = original.LoanwordLearned,
        SuggestOnlyProcesses = Lines(_suggestOnly),
        AlwaysConvertLearned = original.AlwaysConvertLearned,
        MinLength = (int)_minLength.Value,
        ReplaceThreshold = (double)_threshold.Value,
        DictionaryWeight = original.DictionaryWeight,
        NgramWeight = original.NgramWeight,
        ContextWeight = original.ContextWeight,
        DebugLogText = original.DebugLogText,
        LayoutSwitchSettleMs = original.LayoutSwitchSettleMs,
        NeverConvertLearned = original.NeverConvertLearned,
        NeverConvertKey = original.NeverConvertKey,
        ExcludedProcesses = _managed.ExcludedProcesses is null ? Lines(_excluded) : original.ExcludedProcesses,
        NeverConvert = Lines(_neverConvert),
    };
}
