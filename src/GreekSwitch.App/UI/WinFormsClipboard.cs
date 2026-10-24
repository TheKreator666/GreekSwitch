using System.Runtime.InteropServices;
using GreekSwitch.Windows.Output;

namespace GreekSwitch.App.UI;

/// <summary><see cref="IClipboardAccess"/> over the WinForms clipboard (UI thread).</summary>
internal sealed class WinFormsClipboard : IClipboardAccess
{
    private const int Retries = 10;
    private const int RetryDelayMs = 30;

    /// <summary>
    /// Windows' documented opt-outs: clipboard history (Win+V) and cloud clipboard skip the
    /// entry, and clipboard monitors are asked to ignore it.
    /// </summary>
    private static readonly (string Format, byte[] Value)[] PrivacyFormats =
    [
        ("ExcludeClipboardContentFromMonitorProcessing", [0, 0, 0, 0]),
        ("CanIncludeInClipboardHistory", [0, 0, 0, 0]),
        ("CanUploadToCloudClipboard", [0, 0, 0, 0]),
    ];

    public uint SequenceNumber => SelectionKeys.ClipboardSequence();

    public bool TryBackup(out object? backup)
    {
        backup = null;
        var data = Clipboard.GetDataObject();
        var formats = data?.GetFormats(autoConvert: false) ?? [];
        if (data is null || formats.Length == 0)
        {
            return true; // empty: restoring means emptying it again
        }

        var copy = new DataObject();
        foreach (var format in formats)
        {
            try
            {
                if (data.GetData(format, autoConvert: false) is { } value)
                {
                    copy.SetData(format, autoConvert: false, value);
                }
            }
#pragma warning disable CA1031 // some formats can't be read back (delay-rendered, private); keep the rest
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }

        if (copy.GetFormats().Length == 0)
        {
            return false; // content we can't read back: don't overwrite it
        }

        backup = copy;
        return true;
    }

    public void Restore(object? backup)
    {
        try
        {
            if (backup is DataObject data)
            {
                AddPrivacyFormats(data);
                Clipboard.SetDataObject(data, copy: true, Retries, RetryDelayMs);
            }
            else
            {
                Clipboard.Clear();
            }
        }
        catch (ExternalException)
        {
            // Another program holds the clipboard open; the converted text stays on it.
        }
    }

    public string? GetText() => Clipboard.ContainsText(TextDataFormat.UnicodeText) ? Clipboard.GetText(TextDataFormat.UnicodeText) : null;

    public void SetPrivateText(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, autoConvert: false, text);
        AddPrivacyFormats(data);
        Clipboard.SetDataObject(data, copy: true, Retries, RetryDelayMs);
    }

    private static void AddPrivacyFormats(DataObject data)
    {
        foreach (var (format, value) in PrivacyFormats)
        {
            data.SetData(format, autoConvert: false, new MemoryStream(value));
        }
    }
}
