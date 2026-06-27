using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Chromata;

public partial class MagnifierWindow : Window
{
    private const double LoupeSize = 180.0;

    public MagnifierWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Update the loupe with a freshly captured, zoomed bitmap and the sampled colour.
    /// </summary>
    /// <param name="image">The captured NxN region (frozen).</param>
    /// <param name="cellX">Sampled pixel's column within the region.</param>
    /// <param name="cellY">Sampled pixel's row within the region.</param>
    /// <param name="n">Region size in pixels (NxN).</param>
    public void SetContent(ImageSource image, int cellX, int cellY, int n, byte r, byte g, byte b)
    {
        LoupeImage.Source = image;

        double cell = LoupeSize / n;
        double left = cellX * cell;
        double top = cellY * cell;

        CrosshairCell.Width = cell;
        CrosshairCell.Height = cell;
        Canvas.SetLeft(CrosshairCell, left);
        Canvas.SetTop(CrosshairCell, top);

        // Inner 1px black outline gives contrast on both light and dark pixels.
        CrosshairCellInner.Width = cell + 2;
        CrosshairCellInner.Height = cell + 2;
        Canvas.SetLeft(CrosshairCellInner, left - 1);
        Canvas.SetTop(CrosshairCellInner, top - 1);

        var color = Color.FromRgb(r, g, b);
        Swatch.Background = new SolidColorBrush(color);
        HexText.Text = $"#{r:X2}{g:X2}{b:X2}";
        RgbText.Text = $"rgb({r}, {g}, {b})";
    }
}
