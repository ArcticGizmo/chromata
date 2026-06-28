using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using Velopack;
using Velopack.Sources;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using WinForms = System.Windows.Forms;

namespace Chromata;

public partial class App : Application
{
    private const int HotkeyId = 0xC0DE;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Chromata";

    private readonly Settings _settings = Settings.Load();

    private WinForms.NotifyIcon? _tray;
    private WinForms.ToolStripMenuItem? _pickItem;
    private WinForms.ToolStripMenuItem? _recentRoot;
    private HwndSource? _msgWindow;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private bool _updateInProgress;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        CreateMessageWindow();
        CreateTrayIcon();
        RebuildRecentMenu();

        if (!RegisterHotkey(_settings.HotkeyModifiers, _settings.HotkeyVk))
        {
            _tray!.ShowBalloonTip(3000, "Chromata",
                $"Couldn't register {HotkeyDisplay(_settings.HotkeyModifiers, _settings.HotkeyVk)} " +
                "(another app may own it). Pick from the tray menu, or choose another shortcut in Settings.",
                WinForms.ToolTipIcon.Warning);
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (_msgWindow is not null)
        {
            Native.UnregisterHotKey(_msgWindow.Handle, HotkeyId);
            _msgWindow.RemoveHook(WndProc);
            _msgWindow.Dispose();
        }
        _tray?.Dispose();
    }

    // ----- Tray + message window --------------------------------------------

    private void CreateMessageWindow()
    {
        var parameters = new HwndSourceParameters("ChromataMsgWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE -> message-only window
        };
        _msgWindow = new HwndSource(parameters);
        _msgWindow.AddHook(WndProc);
    }

    private void CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();

        _pickItem = new WinForms.ToolStripMenuItem(
            $"Pick a colour\t{HotkeyDisplay(_settings.HotkeyModifiers, _settings.HotkeyVk)}");
        _pickItem.Click += (_, _) => StartPick();
        menu.Items.Add(_pickItem);

        _recentRoot = new WinForms.ToolStripMenuItem("Recent colours");
        menu.Items.Add(_recentRoot);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add("Check for updates…", null, (_, _) => CheckForUpdates());

        var startup = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = IsStartupEnabled(),
        };
        startup.CheckedChanged += (_, _) => SetStartupEnabled(startup.Checked);
        menu.Items.Add(startup);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _tray = new WinForms.NotifyIcon
        {
            Icon = BuildTrayIcon(),
            Visible = true,
            Text = "Chromata — pick a colour from anywhere on screen",
            ContextMenuStrip = menu,
        };
        // Left-click the tray icon to pick; right-click still opens the context menu.
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                StartPick();
        };
    }

    private void RebuildRecentMenu()
    {
        if (_recentRoot is null) return;
        _recentRoot.DropDownItems.Clear();

        if (_settings.History.Count == 0)
        {
            _recentRoot.DropDownItems.Add(new WinForms.ToolStripMenuItem("(no colours yet)") { Enabled = false });
            return;
        }

        foreach (string hex in _settings.History)
        {
            var item = new WinForms.ToolStripMenuItem(hex) { Image = MakeSwatch(hex) };
            string captured = hex;
            item.Click += (_, _) => ReCopy(captured);
            _recentRoot.DropDownItems.Add(item);
        }

        _recentRoot.DropDownItems.Add(new WinForms.ToolStripSeparator());
        var clear = new WinForms.ToolStripMenuItem("Clear history");
        clear.Click += (_, _) => { _settings.History.Clear(); _settings.Save(); RebuildRecentMenu(); };
        _recentRoot.DropDownItems.Add(clear);
    }

    private static Image MakeSwatch(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using var gfx = Graphics.FromImage(bmp);
        using var fill = new SolidBrush(System.Drawing.Color.FromArgb(r, g, b));
        gfx.FillRectangle(fill, 1, 1, 14, 14);
        gfx.DrawRectangle(Pens.Gray, 0, 0, 15, 15);
        return bmp;
    }

    private static Icon BuildTrayIcon()
    {
        // Draw a simple colour-wheel glyph at runtime so we don't ship an .ico file.
        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            var rect = new Rectangle(3, 3, 26, 26);
            g.FillPie(Brushes.OrangeRed, rect, 0, 90);
            g.FillPie(Brushes.Gold, rect, 90, 90);
            g.FillPie(Brushes.MediumSeaGreen, rect, 180, 90);
            g.FillPie(Brushes.DodgerBlue, rect, 270, 90);
            g.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb(230, 27, 27, 31)),
                new Rectangle(11, 11, 10, 10));
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            StartPick();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ----- Hotkey ------------------------------------------------------------

    private bool RegisterHotkey(uint modifiers, uint vk)
    {
        Native.UnregisterHotKey(_msgWindow!.Handle, HotkeyId);
        return Native.RegisterHotKey(_msgWindow.Handle, HotkeyId, modifiers | Native.MOD_NOREPEAT, vk);
    }

    /// <summary>Re-bind the global hotkey. Reverts to the previous combo if the new one is taken.</summary>
    public bool ApplyHotkey(uint modifiers, uint vk)
    {
        uint oldMods = _settings.HotkeyModifiers, oldVk = _settings.HotkeyVk;

        if (!RegisterHotkey(modifiers, vk))
        {
            RegisterHotkey(oldMods, oldVk); // restore the working binding
            _tray?.ShowBalloonTip(3000, "Chromata",
                $"{HotkeyDisplay(modifiers, vk)} is already in use by another app.", WinForms.ToolTipIcon.Warning);
            return false;
        }

        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyVk = vk;
        _settings.Save();
        if (_pickItem is not null)
            _pickItem.Text = $"Pick a colour\t{HotkeyDisplay(modifiers, vk)}";
        return true;
    }

    internal static string HotkeyDisplay(uint mods, uint vk)
    {
        var sb = new StringBuilder();
        if ((mods & Native.MOD_CONTROL) != 0) sb.Append("Ctrl+");
        if ((mods & Native.MOD_ALT) != 0) sb.Append("Alt+");
        if ((mods & Native.MOD_SHIFT) != 0) sb.Append("Shift+");
        if ((mods & Native.MOD_WIN) != 0) sb.Append("Win+");
        sb.Append(KeyInterop.KeyFromVirtualKey((int)vk));
        return sb.ToString();
    }

    // ----- Pick session ------------------------------------------------------

    private void StartPick()
    {
        if (_overlay is not null) return; // already picking

        var shot = new ScreenShot();
        _overlay = new OverlayWindow(shot);
        _overlay.Finished += outcome =>
        {
            _overlay = null;
            if (outcome is not { } o)
            {
                shot.Dispose();
                return;
            }

            if (o.ShowMenu)
            {
                // The overlay (and its loupe) have closed; float the copy-format menu where the
                // user right-clicked. The shot is no longer needed — the colour is already sampled.
                shot.Dispose();
                ShowFormatMenu(o.R, o.G, o.B, o.PhysX, o.PhysY);
            }
            else
            {
                shot.Dispose();
                CommitColor(o.R, o.G, o.B);
            }
        };
        _overlay.Show();
        _overlay.Activate();
    }

    private void CommitColor(byte r, byte g, byte b)
    {
        string text = Settings.FormatColor(_settings.Format, r, g, b);
        Deliver(text, r, g, b);
    }

    /// <summary>Show the right-click menu of every colour format; the chosen one is copied.</summary>
    private void ShowFormatMenu(byte r, byte g, byte b, int physX, int physY)
    {
        var menu = new FormatPickerWindow(r, g, b, physX, physY);
        menu.FormatChosen += text => Deliver(text, r, g, b);
        menu.Show();
        menu.Activate();
    }

    /// <summary>Copy a formatted colour, record it in history, and confirm with a toast.</summary>
    private void Deliver(string text, byte r, byte g, byte b)
    {
        CopyToClipboard(text);

        _settings.AddHistory(Settings.ToHex(r, g, b));
        _settings.Save();
        RebuildRecentMenu();

        ShowToast(text, Color.FromRgb(r, g, b));
    }

    private void ReCopy(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        string text = Settings.FormatColor(_settings.Format, r, g, b);
        CopyToClipboard(text);
        ShowToast(text, Color.FromRgb(r, g, b));
    }

    private static void CopyToClipboard(string text)
    {
        // Clipboard can briefly be locked by another process; retry a few times.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetText(text); return; }
            catch (COMException) { Thread.Sleep(40); }
        }
    }

    private static void ShowToast(string text, Color color)
    {
        try { new ToastWindow(text, color).Show(); }
        catch { /* confirmation is best-effort; the colour is already on the clipboard */ }
    }

    private static (byte R, byte G, byte B) ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return (Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
    }

    // ----- Settings window ---------------------------------------------------

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.HotkeyChanged += (mods, vk) => ApplyHotkey(mods, vk);
        _settingsWindow.FormatChanged += fmt => { _settings.Format = fmt; _settings.Save(); };
        _settingsWindow.HistoryCleared += () => { _settings.History.Clear(); _settings.Save(); RebuildRecentMenu(); };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    // ----- Updates (Velopack) ------------------------------------------------

    private async void CheckForUpdates()
    {
        if (_updateInProgress) return;
        _updateInProgress = true;
        try
        {
            _tray?.ShowBalloonTip(3000, "Chromata", "Checking for updates…", WinForms.ToolTipIcon.Info);

            var mgr = new UpdateManager(new GithubSource(AppInfo.RepoUrl, null, false));
            var update = await mgr.CheckForUpdatesAsync();
            if (update is null)
            {
                _tray?.ShowBalloonTip(4000, "Chromata", "You're on the latest version.", WinForms.ToolTipIcon.Info);
                return;
            }

            _tray?.ShowBalloonTip(5000, "Chromata",
                $"Downloading v{update.TargetFullRelease.Version}…", WinForms.ToolTipIcon.Info);
            await mgr.DownloadUpdatesAsync(update);
            mgr.ApplyUpdatesAndRestart(update); // exits the process
        }
        catch (Exception ex)
        {
            _tray?.ShowBalloonTip(6000, "Chromata — update failed", ex.Message, WinForms.ToolTipIcon.Error);
        }
        finally
        {
            _updateInProgress = false;
        }
    }

    // ----- Run-at-startup ----------------------------------------------------

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    private static void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null) return;
        if (enabled)
        {
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (exe.Length > 0) key.SetValue(RunValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }
}
