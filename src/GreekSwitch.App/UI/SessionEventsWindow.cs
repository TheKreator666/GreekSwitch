using GreekSwitch.Windows.Health;

namespace GreekSwitch.App.UI;

/// <summary>
/// A hidden top-level window that receives lock/unlock (WM_WTSSESSION_CHANGE) and sleep/resume
/// (WM_POWERBROADCAST, which isn't sent to message-only windows).
/// </summary>
internal sealed class SessionEventsWindow : NativeWindow, IDisposable
{
    private const int WtsSessionLock = 0x7;
    private const int WtsSessionUnlock = 0x8;

    private readonly Action<SessionChange> _onChange;

    public SessionEventsWindow(Action<SessionChange> onChange)
    {
        _onChange = onChange;
        CreateHandle(new CreateParams { Caption = "GreekSwitch session events" });
        SessionNotifications.Register(Handle);
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            SessionNotifications.Unregister(Handle);
            DestroyHandle();
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case SessionNotifications.WmWtsSessionChange when (int)m.WParam == WtsSessionLock:
                _onChange(SessionChange.Locked);
                break;
            case SessionNotifications.WmWtsSessionChange when (int)m.WParam == WtsSessionUnlock:
                _onChange(SessionChange.Unlocked);
                break;
            case SessionNotifications.WmPowerBroadcast when (int)m.WParam == SessionNotifications.PbtApmSuspend:
                _onChange(SessionChange.Suspending);
                break;
            case SessionNotifications.WmPowerBroadcast when SessionNotifications.IsResume(m.WParam):
                _onChange(SessionChange.Resumed);
                break;
        }

        base.WndProc(ref m);
    }
}
