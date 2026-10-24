using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace GreekSwitch.App.UI;

/// <summary>
/// Draws the tray icon ("αb" on a rounded square: blue when active, grey when off or paused, red
/// when something is wrong, green for a moment after a correction) and
/// packs it into a multi-size .ico in memory, so no handles leak and no image files are needed.
/// </summary>
internal static class IconFactory
{
    private static readonly int[] Sizes = [16, 20, 24, 32, 48, 256];

    public static Icon Create(bool active, bool error = false, bool flash = false)
    {
        using var stream = new MemoryStream();
        WriteIco(stream, active, error, flash);
        stream.Position = 0;
        return new Icon(stream);
    }

    public static void WriteIco(Stream output, bool active, bool error = false, bool flash = false)
    {
        var images = Sizes.Select(size => (size, png: RenderPng(size, active, error, flash))).ToList();
        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        // ICONDIR, then one ICONDIRENTRY per size, then PNG payloads (supported since Vista).
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);
        var offset = 6 + (16 * images.Count);
        foreach (var (size, png) in images)
        {
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);   // palette
            writer.Write((byte)0);   // reserved
            writer.Write((ushort)1); // planes
            writer.Write((ushort)32); // bits per pixel
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in images)
        {
            writer.Write(png);
        }
    }

    private static byte[] RenderPng(int size, bool active, bool error, bool flash)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var radius = size / 4f;
            using var path = new GraphicsPath();
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(size - (radius * 2) - 1, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(size - (radius * 2) - 1, size - (radius * 2) - 1, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, size - (radius * 2) - 1, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            var color = error ? Color.FromArgb(0xC4, 0x2B, 0x1C) : flash ? Color.FromArgb(0x10, 0x8A, 0x3E) : active ? Color.FromArgb(0x0D, 0x5E, 0xAF) : Color.FromArgb(0x80, 0x80, 0x80);
            using var fill = new SolidBrush(color);
            g.FillPath(fill, path);

            using var font = new Font("Segoe UI", size * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.White);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("αb", font, text, new RectangleF(0, -size * 0.04f, size, size), format);
        }

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        return png.ToArray();
    }
}
