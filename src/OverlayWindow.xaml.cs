using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Chromata;

/// <summary>
/// A full-screen, frozen-desktop overlay (Snip style). The user moves over it with a
/// crosshair and a magnifier loupe; left-click commits the colour, Esc / right-click cancels.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int RegionSize = 21;          // N x N zoomed pixels
    private const int LoupeGap = 28;            // physical px offset from the cursor

    private readonly ScreenShot _shot;
    private readonly MagnifierWindow _loupe = new();

    /// <summary>Fires with the chosen colour, or null if cancelled. Always fires once.</summary>
    public event Action<(byte R, byte G, byte B)?>? Finished;

    private bool _done;

    public OverlayWindow(ScreenShot shot)
    {
        _shot = shot;
        InitializeComponent();
        Frozen.Source = shot.Image;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Size/position the window to cover the whole virtual desktop in physical pixels.
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST,
            _shot.Left, _shot.Top, _shot.Width, _shot.Height,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

        // Prepare the loupe as a no-activate, no-taskbar topmost helper.
        var loupeHwnd = new WindowInteropHelper(_loupe).EnsureHandle();
        int ex = Native.GetWindowLong(loupeHwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(loupeHwnd, Native.GWL_EXSTYLE,
            ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
        _loupe.Show();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        Focus();
        if (Native.GetCursorPos(out var pt)) UpdateLoupe(pt.X, pt.Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        // Use the physical cursor position directly — unambiguous across mixed-DPI monitors.
        if (Native.GetCursorPos(out var pt)) UpdateLoupe(pt.X, pt.Y);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        // Consume the press and keep the overlay open until the release. Committing on mouse-up
        // ensures the overlay window swallows BOTH the down and the up, so neither reaches (or
        // activates) whatever is under the cursor once we close.
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (Native.GetCursorPos(out var pt))
            Commit(_shot.GetColor(pt.X, pt.Y));
        else
            Commit(null);
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        Commit(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Commit(null);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Lost focus (e.g. alt-tab) — treat as cancel so we never get stuck on top.
        Commit(null);
    }

    private void UpdateLoupe(int cursorX, int cursorY)
    {
        var img = _shot.CropZoom(cursorX, cursorY, RegionSize, out int cellX, out int cellY);
        var (r, g, b) = _shot.GetColor(cursorX, cursorY);
        _loupe.SetContent(img, cellX, cellY, RegionSize, r, g, b);
        PositionLoupe(cursorX, cursorY);
    }

    private void PositionLoupe(int cursorX, int cursorY)
    {
        var hwnd = new WindowInteropHelper(_loupe).Handle;
        if (!Native.GetWindowRect(hwnd, out var rect)) return;
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;

        int vsRight = _shot.Left + _shot.Width;
        int vsBottom = _shot.Top + _shot.Height;

        int x = cursorX + LoupeGap;
        int y = cursorY + LoupeGap;
        if (x + w > vsRight) x = cursorX - LoupeGap - w;
        if (y + h > vsBottom) y = cursorY - LoupeGap - h;
        x = Math.Clamp(x, _shot.Left, Math.Max(_shot.Left, vsRight - w));
        y = Math.Clamp(y, _shot.Top, Math.Max(_shot.Top, vsBottom - h));

        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    private void Commit((byte R, byte G, byte B)? color)
    {
        if (_done) return;
        _done = true;
        _loupe.Close();
        Close();
        Finished?.Invoke(color);
    }
}
