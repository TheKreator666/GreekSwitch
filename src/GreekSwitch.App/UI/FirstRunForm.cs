using GreekSwitch.App.Settings;

namespace GreekSwitch.App.UI;

/// <summary>
/// Shown once on a new install: are both layouts installed, what is captured, the hotkeys, and
/// Start with Windows (off by default). Inline text only: the app has no network access.
/// </summary>
internal sealed class FirstRunForm : Form
{
    private const string AddLayoutHelp =
        "Add it in Windows Settings → Time & language → Language & region: add the language "
        + "(or open its Language options) and add its keyboard. GreekSwitch can't correct anything until both are installed.";

    private const string CapturedText =
        "GreekSwitch reads the keys of the word you are typing, in memory only, to tell whether it was "
        + "typed in the wrong layout. It has no network access, writes no typed text to its log, and "
        + "stores words it learns from your corrections only as unreadable hashes. It stays passive in "
        + "password fields, password managers, terminals, remote desktops and the apps you exclude. "
        + "SECURITY.md (with the app's source) has the details.";

    private readonly CheckBox _startWithWindows = new() { Text = "Start GreekSwitch with &Windows", AutoSize = true };
    private readonly Font _bold;

    public FirstRunForm(bool greekInstalled, bool englishInstalled, AppSettings settings, IReadOnlyList<string> unavailableHotkeys)
    {
        Text = "Welcome to GreekSwitch";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Font = SystemFonts.MessageBoxFont ?? Font;
        _bold = new Font(Font, FontStyle.Bold);
        TopMost = true; // a tray app has no window of its own to come up in front of

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        layout.Controls.Add(Heading("Keyboard layouts"));
        layout.Controls.Add(Paragraph(LayoutLine("Greek", greekInstalled)));
        layout.Controls.Add(Paragraph(LayoutLine("English", englishInstalled)));
        if (!greekInstalled || !englishInstalled)
        {
            layout.Controls.Add(Paragraph(AddLayoutHelp));
        }

        layout.Controls.Add(Heading("What is captured"));
        layout.Controls.Add(Paragraph(CapturedText));

        layout.Controls.Add(Heading("Hotkeys (change them in Settings)"));
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var keys = AppHotkeys.Text(settings, action);
            layout.Controls.Add(Paragraph($"{AppHotkeys.Name(action)}: {(string.IsNullOrWhiteSpace(keys) ? "off" : keys)}"));
        }

        layout.Controls.Add(Paragraph("Backspace right after a correction undoes it."));
        if (unavailableHotkeys.Count > 0)
        {
            layout.Controls.Add(Paragraph("Already used by another program: " + string.Join(", ", unavailableHotkeys) + "."));
        }

        layout.Controls.Add(_startWithWindows);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right };
        AcceptButton = ok;
        CancelButton = ok;
        layout.Controls.Add(ok);
        Controls.Add(layout);
    }

    public bool StartWithWindows => _startWithWindows.Checked;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bold.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string LayoutLine(string language, bool installed) =>
        installed ? $"✓ {language} layout installed" : $"✗ No {language} layout installed";

    private Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = _bold,
        Margin = new Padding(3, 12, 3, 3),
    };

    private static Label Paragraph(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(460, 0) };
}
