using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;

namespace Chromata;

public partial class SettingsWindow : Window
{
    public event Action<uint, uint>? HotkeyChanged;
    public event Action<ColorFormat>? FormatChanged;
    public event Action? HistoryCleared;

    private bool _loading;

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();

        _loading = true;
        HotkeyBox.Text = App.HotkeyDisplay(settings.HotkeyModifiers, settings.HotkeyVk);
        (settings.Format switch
        {
            ColorFormat.HexLower => RbHexLower,
            ColorFormat.Rgb => RbRgb,
            ColorFormat.Hsl => RbHsl,
            _ => RbHexUpper,
        }).IsChecked = true;
        PopulateHistory(settings.History);
        _loading = false;
    }

    private void PopulateHistory(IEnumerable<string> history)
    {
        HistoryList.Items.Clear();
        foreach (string hex in history)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(ToColor(hex)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
            });
            panel.Children.Add(new TextBlock
            {
                Text = hex,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas, Cascadia Mono"),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            HistoryList.Items.Add(new ListBoxItem { Content = panel });
        }
    }

    private static Color ToColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromRgb(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore standalone modifier presses — wait for the actual key.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        uint mods = 0;
        ModifierKeys m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) mods |= Native.MOD_CONTROL;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= Native.MOD_ALT;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= Native.MOD_SHIFT;

        // A reliable global hotkey needs Ctrl or Alt (Shift-only combos are flaky).
        if ((mods & (Native.MOD_CONTROL | Native.MOD_ALT)) == 0)
        {
            HotkeyBox.Text = "Include Ctrl or Alt…";
            return;
        }

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        HotkeyBox.Text = App.HotkeyDisplay(mods, vk);
        HotkeyChanged?.Invoke(mods, vk);
    }

    private void Format_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is RadioButton { Tag: string tag } && Enum.TryParse(tag, out ColorFormat fmt))
            FormatChanged?.Invoke(fmt);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        HistoryCleared?.Invoke();
        HistoryList.Items.Clear();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
