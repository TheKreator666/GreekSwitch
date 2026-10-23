using GreekSwitch.Core;
using GreekSwitch.Core.Input;
using GreekSwitch.Windows.Layouts;

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
/// <param name="Layout">For keys: the keyboard layout (HKL) of the focused window when the key was pressed.</param>
public readonly record struct InputEvent(InputEventKind Kind, KeyInput Key = default, nint Window = 0, string? ErrorType = null, long TypedCount = 0, nint Layout = 0)
{
    /// <param name="typedCount">TypeAheadGuard count when the key was captured.</param>
    /// <param name="focusWindow">The control that had keyboard focus when the key was pressed.</param>
    /// <param name="layout">The focused window's keyboard layout (HKL) at that moment.</param>
    public static InputEvent ForKey(KeyInput key, long typedCount, nint focusWindow, nint layout) =>
        new(InputEventKind.Key, key, focusWindow, TypedCount: typedCount, Layout: layout);

    /// <summary>
    /// The language of the layout the key was typed with; <paramref name="fallback"/> when the hook
    /// couldn't read it. Null means another language's layout was active.
    /// </summary>
    public Lang? KeyLanguage(Func<Lang?> fallback) => Layout != 0 ? KeyboardLayouts.ToLang(Layout) : fallback();

    public static InputEvent Click() => new(InputEventKind.MouseClick);

    public static InputEvent Foreground(nint hwnd) => new(InputEventKind.ForegroundChanged, Window: hwnd);

    public static InputEvent Focus(nint hwnd) => new(InputEventKind.FocusChanged, Window: hwnd);

    public static InputEvent Error(Exception ex) => new(InputEventKind.HookError, ErrorType: ex.GetType().Name);
}
