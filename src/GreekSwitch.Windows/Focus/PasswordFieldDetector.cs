using System.Runtime.InteropServices;

namespace GreekSwitch.Windows.Focus;

public interface IPasswordFieldDetector
{
    /// <summary>The focused element: is it a password box, and which window does it belong to.</summary>
    PasswordCheck CheckFocused();
}

/// <summary>
/// UI Automation check: is the focused element a password box? Runs on the
/// <see cref="UiaThread"/> after every focus change; until it answers, and while it says yes,
/// GreekSwitch captures nothing.
/// </summary>
public sealed class PasswordFieldDetector : IPasswordFieldDetector
{
    private readonly Lazy<IUIAutomation> _automation = new(() => (IUIAutomation)new CUIAutomation());

    public PasswordCheck CheckFocused() => Check(() => _automation.Value.GetFocusedElement());

    /// <summary>Same check for a specific window, e.g. a control's handle (used by tests).</summary>
    public PasswordCheck CheckWindow(nint hwnd) => Check(() => _automation.Value.ElementFromHandle(hwnd));

    private static PasswordCheck Check(Func<IUIAutomationElement?> find)
    {
        IUIAutomationElement? element = null;
        try
        {
            element = find();
            if (element is null)
            {
                return new PasswordCheck(false, 0);
            }

            var isPassword = element.GetCurrentPropertyValue(Uia.IsPasswordPropertyId) is true;
            var window = element.GetCurrentPropertyValue(Uia.NativeWindowHandlePropertyId) is int handle ? (nint)handle : 0;
            return new PasswordCheck(isPassword, window);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException)
        {
            // Focus moved again, or the app doesn't expose UI Automation: no password box we can see.
            return new PasswordCheck(false, 0);
        }
        finally
        {
            Uia.Release(element);
        }
    }
}
