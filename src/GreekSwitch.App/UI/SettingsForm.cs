using GreekSwitch.App.Settings;

namespace GreekSwitch.App.UI;

/// <summary>The settings dialog, built in code. Edits a copy; the caller applies it on OK.</summary>
internal sealed class SettingsForm : Form
{
    private readonly CheckBox _enabled = new() { Text = "&Enabled", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with &Windows", AutoSize = true };
    private readonly CheckBox _correctOnEnter = new() { Text = "Also correct words ended by Enter or &Tab", AutoSize = true };
    private readonly NumericUpDown _minLength = new() { Minimum = 1, Maximum = 20, Width = 70 };
    private readonly NumericUpDown _threshold = new() { Minimum = 0, Maximum = 20, DecimalPlaces = 1, Increment = 0.5m, Width = 70 };
    private readonly TextBox _excluded = MultilineBox();
    private readonly TextBox _neverConvert = MultilineBox();

    public SettingsForm(AppSettings settings, bool startWithWindows)
    {
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

        _enabled.Checked = settings.Enabled;
        _startWithWindows.Checked = startWithWindows;
        _correctOnEnter.Checked = settings.CorrectOnEnterAndTab;
        _minLength.Value = settings.MinLength;
        _threshold.Value = (decimal)settings.ReplaceThreshold;
        _excluded.Text = string.Join(Environment.NewLine, settings.ExcludedProcesses);
        _neverConvert.Text = string.Join(Environment.NewLine, settings.NeverConvert);
        Result = settings;

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var restore = new Button { Text = "Restore default exclusions", AutoSize = true };
        restore.Click += (_, _) => _excluded.Text = string.Join(Environment.NewLine, AppSettings.DefaultExcludedProcesses);
        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, _) => Result = Collect(settings);

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddRow(layout, _enabled);
        AddRow(layout, _startWithWindows);
        AddRow(layout, _correctOnEnter);
        AddRow(layout, Label("Minimum word length (keys):"), _minLength);
        AddRow(layout, Label("Replace threshold (score margin):"), _threshold);
        AddRow(layout, Label("Excluded apps (one .exe per line):"));
        AddRow(layout, _excluded);
        AddRow(layout, restore);
        AddRow(layout, Label("Never convert these words (one per line; undo adds to this list):"));
        AddRow(layout, _neverConvert);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        AddRow(layout, buttons);
        Controls.Add(layout);
    }

    public AppSettings Result { get; private set; }

    public bool StartWithWindows => _startWithWindows.Checked;

    private static TextBox MultilineBox() => new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Width = 420,
        Height = 110,
        AcceptsReturn = true,
    };

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

    private AppSettings Collect(AppSettings original) => new()
    {
        Enabled = _enabled.Checked,
        CorrectOnEnterAndTab = _correctOnEnter.Checked,
        MinLength = (int)_minLength.Value,
        ReplaceThreshold = (double)_threshold.Value,
        DictionaryWeight = original.DictionaryWeight,
        NgramWeight = original.NgramWeight,
        ContextWeight = original.ContextWeight,
        DebugLogText = original.DebugLogText,
        LayoutSwitchSettleMs = original.LayoutSwitchSettleMs,
        ExcludedProcesses = Lines(_excluded),
        NeverConvert = Lines(_neverConvert),
    };
}
