using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace GreekSwitch.Windows.Focus;

public interface IPasswordFieldDetector
{
    bool IsPasswordFieldFocused();
}

/// <summary>
/// UI Automation check: is the focused element a password box? Checked on every focus change;
/// while it is, GreekSwitch captures nothing.
/// </summary>
public sealed class PasswordFieldDetector : IPasswordFieldDetector
{
    public bool IsPasswordFieldFocused()
    {
        try
        {
            return AutomationElement.FocusedElement is { } element && element.Current.IsPassword;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or TimeoutException)
        {
            // Focus moved again or the app doesn't expose UIA: no password box we can see.
            return false;
        }
    }
}
