using System.Runtime.InteropServices;

namespace GreekSwitch.Windows.Focus;

public interface IPasswordFieldDetector
{
    bool IsPasswordFieldFocused();
}

/// <summary>
/// UI Automation check: is the focused element a password box? Checked on every focus change,
/// and while it is, GreekSwitch captures nothing. Talks to the OS UIAutomationCore through COM
/// directly rather than through WPF's managed wrapper: that avoids shipping WPF, whose native
/// DLLs a single-file exe would have to unpack into a user-writable temp folder.
/// </summary>
public sealed class PasswordFieldDetector : IPasswordFieldDetector
{
    private const int UiaIsPasswordPropertyId = 30019;

    private readonly Lazy<IUIAutomation> _automation = new(() => (IUIAutomation)new CUIAutomation());

    public bool IsPasswordFieldFocused()
    {
        try
        {
            return IsPassword(_automation.Value.GetFocusedElement());
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException)
        {
            // Focus moved again, or the app doesn't expose UI Automation: no password box we can see.
            return false;
        }
    }

    /// <summary>Same check for a specific window, e.g. a control's handle (used by tests).</summary>
    public bool IsPasswordWindow(nint hwnd)
    {
        try
        {
            return IsPassword(_automation.Value.ElementFromHandle(hwnd));
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsPassword(IUIAutomationElement? element)
    {
        if (element is null)
        {
            return false;
        }

        try
        {
            return element.GetCurrentPropertyValue(UiaIsPasswordPropertyId) is true;
        }
        finally
        {
            Marshal.ReleaseComObject(element);
        }
    }

#pragma warning disable SA1201, CA1812, IDE0051 // COM interop declarations

    [ComImport]
    [Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
    private class CUIAutomation
    {
    }

    /// <summary>IUIAutomation, declared up to the methods we call; earlier slots keep the vtable order.</summary>
    [ComImport]
    [Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation
    {
        void CompareElements();

        void CompareRuntimeIds();

        void GetRootElement();

        IUIAutomationElement ElementFromHandle(nint hwnd);

        void ElementFromPoint();

        IUIAutomationElement GetFocusedElement();
    }

    /// <summary>IUIAutomationElement, declared up to GetCurrentPropertyValue (8th method).</summary>
    [ComImport]
    [Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement
    {
        void SetFocus();

        void GetRuntimeId();

        void FindFirst();

        void FindAll();

        void FindFirstBuildCache();

        void FindAllBuildCache();

        void BuildUpdatedCache();

        [return: MarshalAs(UnmanagedType.Struct)]
        object? GetCurrentPropertyValue(int propertyId);
    }

#pragma warning restore SA1201, CA1812, IDE0051
}
