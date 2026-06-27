using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Chromata;

/// <summary>
/// A frozen snapshot of the entire virtual desktop (all monitors), in physical pixels.
/// Serves the overlay background image plus per-pixel colour and zoom-region queries,
/// so nothing has to touch the live screen again during a pick.
/// </summary>
public sealed class ScreenShot : IDisposable
{
    public int Left { get; }
    public int Top { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>The whole desktop as a frozen WPF image, for the overlay background.</summary>
    public BitmapSource Image { get; }

    private readonly Bitmap _bmp;

    public ScreenShot()
    {
        Left = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
        Top = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
        Width = Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN);
        Height = Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN);

        _bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(_bmp))
            g.CopyFromScreen(Left, Top, 0, 0, new System.Drawing.Size(Width, Height),
                CopyPixelOperation.SourceCopy);

        Image = ToBitmapSource(_bmp);
    }

    /// <summary>Colour of the pixel at a physical desktop coordinate.</summary>
    public (byte R, byte G, byte B) GetColor(int physX, int physY)
    {
        int x = Math.Clamp(physX - Left, 0, Width - 1);
        int y = Math.Clamp(physY - Top, 0, Height - 1);
        var c = _bmp.GetPixel(x, y);
        return (c.R, c.G, c.B);
    }

    /// <summary>
    /// Crop an N x N region centred on a physical point, clamped to the desktop.
    /// Returns the cropped image plus the sampled pixel's cell within it.
    /// </summary>
    public BitmapSource CropZoom(int physX, int physY, int n, out int cellX, out int cellY)
    {
        int half = n / 2;
        int left = Math.Clamp(physX - Left - half, 0, Math.Max(0, Width - n));
        int top = Math.Clamp(physY - Top - half, 0, Math.Max(0, Height - n));
        cellX = (physX - Left) - left;
        cellY = (physY - Top) - top;

        using var crop = _bmp.Clone(new Rectangle(left, top, n, n), _bmp.PixelFormat);
        return ToBitmapSource(crop);
    }

    private static BitmapSource ToBitmapSource(Bitmap bmp)
    {
        IntPtr hbmp = bmp.GetHbitmap();
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(
                hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            Native.DeleteObject(hbmp);
        }
    }

    public void Dispose() => _bmp.Dispose();
}
