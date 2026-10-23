using System.Runtime.InteropServices;

namespace GreekSwitch.Windows.Focus;

#pragma warning disable SA1201, CA1812, IDE0051 // COM interop declarations

// Minimal UI Automation COM declarations (UIAutomationClient.h), called through the OS's
// UIAutomationCore rather than WPF's managed wrapper. Each interface is declared up to the
// last method we call; earlier methods are placeholders that keep the vtable order and are
// never called.

[ComImport]
[Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
internal class CUIAutomation
{
}

[ComImport]
[Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomation
{
    void CompareElements();

    void CompareRuntimeIds();

    void GetRootElement();

    IUIAutomationElement ElementFromHandle(nint hwnd);

    void ElementFromPoint();

    IUIAutomationElement GetFocusedElement();
}

[ComImport]
[Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationElement
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

    void GetCurrentPropertyValueEx();

    void GetCachedPropertyValue();

    void GetCachedPropertyValueEx();

    void GetCurrentPatternAs();

    void GetCachedPatternAs();

    [return: MarshalAs(UnmanagedType.IUnknown)]
    object? GetCurrentPattern(int patternId);
}

[ComImport]
[Guid("32eba289-3583-42c9-9c59-3b6d9a1e9b6a")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationTextPattern
{
    void RangeFromPoint();

    void RangeFromChild();

    IUIAutomationTextRangeArray GetSelection();
}

[ComImport]
[Guid("ce4ae76a-e717-4c98-81ea-47371d028eb6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationTextRangeArray
{
    int Length { get; }

    IUIAutomationTextRange GetElement(int index);
}

/// <summary>
/// Declared under IUIAutomationTextRange2's IID (Windows 8.1+), which extends
/// IUIAutomationTextRange with one method at the end, so the methods below keep their slots.
/// Text ranges returned by UIAutomationCore answer QueryInterface for TextRange2/3; the
/// base-interface IID we first used was refused (E_NOINTERFACE), found by a live test.
/// </summary>
[ComImport]
[Guid("bb9b40e0-5e04-46bd-9be0-4b601b9afad4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationTextRange
{
    IUIAutomationTextRange Clone();

    void Compare();

    void CompareEndpoints();

    void ExpandToEnclosingUnit();

    void FindAttribute();

    void FindText();

    void GetAttributeValue();

    void GetBoundingRectangles();

    void GetEnclosingElement();

    [return: MarshalAs(UnmanagedType.BStr)]
    string GetText(int maxLength);

    void Move();

    int MoveEndpointByUnit(int endpoint, int unit, int count);

    void MoveEndpointByRange(int sourceEndpoint, IUIAutomationTextRange targetRange, int targetEndpoint);
}

internal static class Uia
{
    public const int IsPasswordPropertyId = 30019;
    public const int NativeWindowHandlePropertyId = 30020;
    public const int TextPatternId = 10014;
    public const int EndpointStart = 0;
    public const int EndpointEnd = 1;
    public const int UnitCharacter = 0;

    public static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
        {
            Marshal.ReleaseComObject(com);
        }
    }
}

#pragma warning restore SA1201, CA1812, IDE0051
