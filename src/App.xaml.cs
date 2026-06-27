using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using WinForms = System.Windows.Forms;

namespace Chromata;

public partial class App : Application
{
    private const int HotkeyId = 0xC0DE;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Chromata";

    private WinForms.NotifyIcon? _tray;
    private HwndSource? _msgWindow;
    private OverlayWindow? _overlay;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        CreateMessageWindow();
        CreateTrayIcon();

        // Global "pick" hotkey: Ctrl+Alt+C.
        if (!Native.RegisterHotKey(_msgWindow!.Handle, HotkeyId,
                Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, 0x43 /* 'C' */))
        {
            _tray!.ShowBalloonTip(3000, "Chromata",
                "Couldn't register Ctrl+Alt+C (another app may own it). Use the tray menu to pick.",
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
        menu.Items.Add("Pick a colour\tCtrl+Alt+C", null, (_, _) => StartPick());
        menu.Items.Add(new WinForms.ToolStripSeparator());

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
            Text = "Chromata — Ctrl+Alt+C to pick a colour",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => StartPick();
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

    // ----- Pick session ------------------------------------------------------

    private void StartPick()
    {
        if (_overlay is not null) return; // already picking

        var shot = new ScreenShot();
        _overlay = new OverlayWindow(shot);
        _overlay.Finished += color =>
        {
            _overlay = null;
            shot.Dispose();
            if (color is { } c) CommitColor(c.R, c.G, c.B);
        };
        _overlay.Show();
        _overlay.Activate();
    }

    private void CommitColor(byte r, byte g, byte b)
    {
        string hex = $"#{r:X2}{g:X2}{b:X2}";

        // Clipboard can briefly be locked by another process; retry a few times.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetText(hex); break; }
            catch (COMException) { System.Threading.Thread.Sleep(40); }
        }

        _tray?.ShowBalloonTip(1200, "Chromata", $"{hex} copied to clipboard", WinForms.ToolTipIcon.None);
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
