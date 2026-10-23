using GreekSwitch.Core.Input;

namespace GreekSwitch.Windows.Hooks;

public enum InputEventKind
{
    Key,
    MouseClick,
    ForegroundChanged,
    FocusChanged,
    PolicyChanged,
    HookError,
}

/// <summary>What the hook thread hands to the worker. Small, allocation-free, enqueued in microseconds.</summary>
public readonly record struct InputEvent(InputEventKind Kind, KeyInput Key = default, nint Window = 0, string? ErrorType = null, long TypedCount = 0)
{
    /// <param name="typedCount">TypeAheadGuard count when the key was captured.</param>
    public static InputEvent ForKey(KeyInput key, long typedCount) => new(InputEventKind.Key, key, TypedCount: typedCount);

    public static InputEvent Click() => new(InputEventKind.MouseClick);

    public static InputEvent Foreground(nint hwnd) => new(InputEventKind.ForegroundChanged, Window: hwnd);

    public static InputEvent Focus(nint hwnd) => new(InputEventKind.FocusChanged, Window: hwnd);

    public static InputEvent Error(Exception ex) => new(InputEventKind.HookError, ErrorType: ex.GetType().Name);
}
