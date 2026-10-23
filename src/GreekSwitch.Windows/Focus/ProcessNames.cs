using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Focus;

public static class ProcessNames
{
    /// <summary>
    /// Executable file name of the process owning <paramref name="hwnd"/>, e.g. "notepad.exe",
    /// or null if it can't be read (e.g. a protected process).
    /// </summary>
    public static unsafe string? FromWindow(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0)
        {
            return null;
        }

        try
        {
            var buffer = stackalloc char[1024];
            var size = 1024u;
            return QueryFullProcessImageNameW(process, 0, buffer, ref size)
                ? System.IO.Path.GetFileName(new string(buffer, 0, (int)size))
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }
}
