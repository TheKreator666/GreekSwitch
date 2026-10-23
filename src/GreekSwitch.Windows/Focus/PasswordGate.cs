namespace GreekSwitch.Windows.Focus;

/// <summary>A focused element as reported by EVENT_OBJECT_FOCUS: its window and child id (0 = the window itself).</summary>
public readonly record struct FocusKey(nint Window, int ChildId);

/// <summary>A UI Automation answer about the focused element.</summary>
/// <param name="IsPassword">The element is a password box.</param>
/// <param name="NativeWindow">The element's own window handle (0 for windowless elements, e.g. web page fields).</param>
public readonly record struct PasswordCheck(bool IsPassword, nint NativeWindow);

/// <summary>
/// Decides whether capture must be blocked because a password box may have focus. Fails closed:
/// after a focus change, capture stays blocked until UI Automation answers, and stays blocked if
/// it never does. Answers are cached per window only when they are provably about that window's
/// own control (a native control, child id 0). Browsers and Electron apps reuse one window for
/// every field on a page, so caching there could let a "not a password" answer for one field
/// unblock a password field later.
/// </summary>
public sealed class PasswordGate
{
    private const int MaxCached = 512;

    private readonly Dictionary<nint, bool> _cache = [];

    /// <summary>The element that has focus now.</summary>
    public FocusKey Current { get; private set; }

    /// <summary>True while a password box may have focus: capture must stay off.</summary>
    public bool Blocked { get; private set; } = true;

    /// <summary>Blocked only because the answer for the current element hasn't arrived yet.</summary>
    public bool Pending { get; private set; } = true;

    /// <summary>
    /// Focus moved to <paramref name="key"/>. Returns true if a UI Automation check must be
    /// started (the gate is then blocked until <see cref="OnAnswer"/>).
    /// </summary>
    /// <param name="wouldCapture">Capture would be on apart from password boxes (enabled, not paused, not excluded).</param>
    public bool OnFocus(FocusKey key, bool wouldCapture)
    {
        Current = key;
        if (!wouldCapture)
        {
            Blocked = false; // capture is off anyway; nothing to protect, nothing to query
            Pending = false;
            return false;
        }

        if (key.ChildId == 0 && _cache.TryGetValue(key.Window, out var isPassword))
        {
            Blocked = isPassword;
            Pending = false;
            return false;
        }

        Blocked = true;
        Pending = true;
        return true;
    }

    /// <summary>
    /// UI Automation answered (or timed out, <paramref name="check"/> null) for <paramref name="key"/>.
    /// Returns true if the blocked state may have changed.
    /// </summary>
    public bool OnAnswer(FocusKey key, PasswordCheck? check)
    {
        if (check is { } answer && key.ChildId == 0 && answer.NativeWindow == key.Window && key.Window != 0)
        {
            if (_cache.Count >= MaxCached)
            {
                _cache.Clear();
            }

            _cache[key.Window] = answer.IsPassword;
        }

        if (key != Current)
        {
            return false; // focus has moved on; a check for the new element is under way
        }

        Blocked = check is not { IsPassword: false }; // a timeout keeps it closed
        Pending = false;
        return true;
    }
}
