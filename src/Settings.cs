using System.IO;
using System.Text.Json;

namespace Chromata;

public enum ColorFormat
{
    HexLower,   // #2e8b57
    HexUpper,   // #2E8B57
    Rgb,        // rgb(46, 139, 87)
    Hsl,        // hsl(146, 50%, 36%)
}

/// <summary>
/// User settings persisted as JSON in %LOCALAPPDATA%\Chromata\settings.json.
/// </summary>
public sealed class Settings
{
    private const int HistoryLimit = 16;

    // Global hotkey, stored as Win32 modifier flags + virtual-key code. Default Ctrl+Alt+C.
    public uint HotkeyModifiers { get; set; } = Native.MOD_CONTROL | Native.MOD_ALT;
    public uint HotkeyVk { get; set; } = 0x43; // 'C'

    public ColorFormat Format { get; set; } = ColorFormat.HexUpper;

    /// <summary>Most-recent-first list of canonical "#RRGGBB" picks.</summary>
    public List<string> History { get; set; } = new();

    // ----- Persistence -------------------------------------------------------

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Chromata");
    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* fall back to defaults on any read/parse error */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* settings are best-effort; never crash the app over them */ }
    }

    public void AddHistory(string hex)
    {
        History.RemoveAll(h => string.Equals(h, hex, StringComparison.OrdinalIgnoreCase));
        History.Insert(0, hex);
        if (History.Count > HistoryLimit)
            History.RemoveRange(HistoryLimit, History.Count - HistoryLimit);
    }

    // ----- Colour formatting -------------------------------------------------

    /// <summary>Canonical history key, independent of the chosen display format.</summary>
    public static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    public static string FormatColor(ColorFormat fmt, byte r, byte g, byte b) => fmt switch
    {
        ColorFormat.HexLower => $"#{r:x2}{g:x2}{b:x2}",
        ColorFormat.HexUpper => $"#{r:X2}{g:X2}{b:X2}",
        ColorFormat.Rgb => $"rgb({r}, {g}, {b})",
        ColorFormat.Hsl => FormatHsl(r, g, b),
        _ => $"#{r:X2}{g:X2}{b:X2}",
    };

    private static string FormatHsl(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf));
        double min = Math.Min(rf, Math.Min(gf, bf));
        double h = 0, s, l = (max + min) / 2.0;
        double d = max - min;

        if (d == 0)
        {
            s = 0;
        }
        else
        {
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == rf) h = (gf - bf) / d + (gf < bf ? 6 : 0);
            else if (max == gf) h = (bf - rf) / d + 2;
            else h = (rf - gf) / d + 4;
            h /= 6;
        }

        return $"hsl({Math.Round(h * 360)}, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%)";
    }
}
