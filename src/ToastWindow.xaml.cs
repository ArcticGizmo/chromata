using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;

namespace Chromata;

/// <summary>
/// A small, self-closing confirmation pill shown after a colour is copied.
/// Replaces the tray balloon now that the app is non-resident.
/// </summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1100) };

    public ToastWindow(string hex, Color color)
    {
        InitializeComponent();
        Label.Text = $"{hex} copied";
        Swatch.Background = new SolidColorBrush(color);

        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Sit near the bottom-centre of the primary monitor's work area.
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - ActualWidth) / 2;
        Top = wa.Bottom - ActualHeight - 48;
        _timer.Start();
    }
}
