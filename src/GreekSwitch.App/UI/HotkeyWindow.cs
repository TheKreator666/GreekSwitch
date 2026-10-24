using GreekSwitch.App.Settings;
using GreekSwitch.Windows;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.UI;

/// <summary>
/// A message-only window on the UI thread that owns the app's hotkeys (RegisterHotKey) and turns
/// WM_HOTKEY into <see cref="HotkeyAction"/>s. Nothing here runs in the keyboard hook.
/// </summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private static readonly nint MessageOnly = -3; // HWND_MESSAGE

    private readonly Action<HotkeyAction> _onHotkey;
    private readonly ILogger _logger;
    private readonly List<HotkeyAction> _registered = [];
    private readonly List<string> _unavailable = [];

    public HotkeyWindow(Action<HotkeyAction> onHotkey, ILogger logger)
    {
        _onHotkey = onHotkey;
        _logger = logger;
        CreateHandle(new CreateParams { Caption = "GreekSwitch hotkeys", Parent = MessageOnly });
    }

    /// <summary>Hotkeys from the last <see cref="Register"/> that another program already owns ("Convert selection (Ctrl+Alt+Shift+Space)").</summary>
    public IReadOnlyList<string> Unavailable => _unavailable;

    /// <summary>Replaces the registered hotkeys with the ones in <paramref name="settings"/>.</summary>
    public void Register(AppSettings settings)
    {
        UnregisterAll();
        _unavailable.Clear();
        foreach (var (action, chord) in AppHotkeys.Parse(settings))
        {
            if (Hotkeys.Register(Handle, (int)action, chord))
            {
                _registered.Add(action);
            }
            else
            {
                _logger.LogWarning("Hotkey for '{Action}' is already used by another program", AppHotkeys.Name(action));
                _unavailable.Add($"{AppHotkeys.Name(action)} ({AppHotkeys.Text(settings, action)})");
            }
        }
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            UnregisterAll();
            DestroyHandle();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Hotkeys.WmHotkey && Enum.IsDefined(typeof(HotkeyAction), (int)m.WParam))
        {
            _onHotkey((HotkeyAction)(int)m.WParam);
        }

        base.WndProc(ref m);
    }

    private void UnregisterAll()
    {
        foreach (var action in _registered)
        {
            Hotkeys.Unregister(Handle, (int)action);
        }

        _registered.Clear();
    }
}
