using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Health;

/// <summary>Lock/unlock and sleep/resume notifications for a window (WM_WTSSESSION_CHANGE, WM_POWERBROADCAST).</summary>
public static class SessionNotifications
{
    public const int WmWtsSessionChange = 0x02B1;
    public const int WmPowerBroadcast = 0x0218;
    public const int PbtApmResumeAutomatic = 0x0012;
    public const int PbtApmResumeSuspend = 0x0007;
    public const int PbtApmSuspend = 0x0004;
    private const uint NotifyForThisSession = 0;

    public static bool Register(nint hwnd) => WTSRegisterSessionNotification(hwnd, NotifyForThisSession);

    public static void Unregister(nint hwnd) => WTSUnRegisterSessionNotification(hwnd);

    /// <summary>True for power events after which the hooks may have been dropped.</summary>
    public static bool IsResume(nint wParam) => wParam is PbtApmResumeAutomatic or PbtApmResumeSuspend;
}
