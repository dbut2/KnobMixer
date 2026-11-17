using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace KnobMixer;

public partial class App : System.Windows.Application
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "KnobMixer";

    Mutex? _single;
    KeyboardHook? _hook;
    System.Windows.Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _single = new Mutex(true, "KnobMixer-single-instance", out bool first);
        if (!first) { Shutdown(); return; }

        var overlay = new OverlayWindow();
        var controller = new Controller(Dispatcher, overlay);
        _hook = new KeyboardHook(controller.OnKey);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        var auto = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { Checked = IsAutostart(), CheckOnClick = true };
        auto.CheckedChanged += (_, _) => SetAutostart(auto.Checked);
        menu.Items.Add(auto);
        menu.Items.Add("Quit", null, (_, _) => Shutdown());

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "KnobMixer - click your knob to open",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _single?.Dispose();
        base.OnExit(e);
    }

    static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(RunName) != null;
    }

    static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (k == null) return;
        if (on) k.SetValue(RunName, "\"" + Environment.ProcessPath + "\"");
        else k.DeleteValue(RunName, false);
    }

    // A simple knob: dark disc with a light notch, drawn so the project needs no .ico file.
    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(new SolidBrush(Color.FromArgb(0x5C, 0xC8, 0xFF)), 2, 2, 28, 28);
            g.FillEllipse(new SolidBrush(Color.FromArgb(0x16, 0x16, 0x1A)), 6, 6, 20, 20);
            g.FillRectangle(Brushes.White, 15, 5, 3, 10);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
