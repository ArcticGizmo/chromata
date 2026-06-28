using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Chromata;

/// <summary>
/// A small dark menu shown when the user right-clicks while picking. Lists every
/// representation of the sampled colour (HEX, RGB, HSL, …) and copies the one clicked.
/// </summary>
public partial class FormatPickerWindow : Window
{
    private const int CursorGap = 12; // physical px between the cursor and the menu corner

    private readonly int _physX;
    private readonly int _physY;
    private bool _done;

    /// <summary>Fires with the chosen format string when a row is clicked (never on cancel).</summary>
    public event Action<string>? FormatChosen;

    public FormatPickerWindow(byte r, byte g, byte b, int physX, int physY)
    {
        InitializeComponent();
        _physX = physX;
        _physY = physY;

        Swatch.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        Rows.ItemsSource = Settings.AllFormats(r, g, b);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        PlaceNearCursor();
    }

    /// <summary>Position the menu by the cursor in physical pixels, clamped to the virtual desktop.</summary>
    private void PlaceNearCursor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!Native.GetWindowRect(hwnd, out var rect)) return;
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;

        int vsLeft = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
        int vsTop = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
        int vsRight = vsLeft + Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN);
        int vsBottom = vsTop + Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN);

        int x = _physX + CursorGap;
        int y = _physY + CursorGap;
        if (x + w > vsRight) x = _physX - CursorGap - w; // flip left if it would overflow
        if (y + h > vsBottom) y = _physY - CursorGap - h; // flip up if it would overflow
        x = Math.Clamp(x, vsLeft, Math.Max(vsLeft, vsRight - w));
        y = Math.Clamp(y, vsTop, Math.Max(vsTop, vsBottom - h));

        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_SHOWWINDOW);
    }

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        if (_done) return;
        if (sender is Button { Tag: string value })
        {
            _done = true;
            FormatChosen?.Invoke(value);
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Clicking away (or alt-tab) dismisses the menu without copying.
        Close();
    }
}
