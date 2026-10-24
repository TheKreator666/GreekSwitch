namespace GreekSwitch.App.UI;

/// <summary>
/// The loanword suggestion next to the caret: "container  Tab". Never takes focus or shows in
/// the taskbar (WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW), and hides itself after a
/// few seconds. Accepting is done by the hook (Tab), not by clicking. UI thread only.
/// </summary>
internal sealed class SuggestionPopup : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTopmost = 0x00000008;
    private const int WsExToolWindow = 0x00000080;
    private const int ShowMs = 4000;

    private readonly Label _text = new() { AutoSize = true, Margin = new Padding(0), Padding = new Padding(6, 3, 2, 3) };
    private readonly Label _hint = new() { AutoSize = true, Text = "Tab ↹", Margin = new Padding(0), Padding = new Padding(2, 3, 6, 3) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = ShowMs };

    public SuggestionPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = SystemColors.Info;
        ForeColor = SystemColors.InfoText;
        Font = SystemFonts.MessageBoxFont ?? Font;
        _hint.ForeColor = SystemColors.GrayText;

        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Padding = new Padding(1), BorderStyle = BorderStyle.FixedSingle };
        row.Controls.Add(_text);
        row.Controls.Add(_hint);
        Controls.Add(row);
        _timer.Tick += (_, _) =>
        {
            HideSuggestion();
            Expired?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Hidden by its timeout (not by a key): the caller closes the hook's gate.</summary>
    public event EventHandler? Expired;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExTopmost | WsExToolWindow;
            return cp;
        }
    }

    /// <summary>Shows <paramref name="target"/> at a screen position (below the caret), kept on that monitor.</summary>
    public void ShowSuggestion(string target, int x, int y)
    {
        _text.Text = target;
        var screen = Screen.FromPoint(new Point(x, y)).WorkingArea;
        var size = PreferredSize;
        Location = new Point(
            Math.Clamp(x, screen.Left, Math.Max(screen.Left, screen.Right - size.Width)),
            y + size.Height > screen.Bottom ? Math.Max(screen.Top, y - (2 * size.Height)) : y);
        if (!Visible)
        {
            Show();
        }

        _timer.Stop();
        _timer.Start();
    }

    public void HideSuggestion()
    {
        _timer.Stop();
        _text.Text = "";
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}
