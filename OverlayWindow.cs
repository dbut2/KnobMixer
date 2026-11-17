using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace KnobMixer;

/// <summary>
/// Borderless always-on-top panel that never takes focus (so a game keeps running underneath)
/// and ignores the mouse. Built in code to keep the project to a handful of files.
/// </summary>
public sealed class OverlayWindow : Window
{
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;
    static readonly IntPtr HWND_TOPMOST = new(-1);
    const uint SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
    const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO info);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    static readonly Brush Panel = Freeze(new SolidColorBrush(Color.FromArgb(0xEE, 0x16, 0x16, 0x1A)));
    static readonly Brush TileIdle = Freeze(new SolidColorBrush(Color.FromArgb(0x00, 0, 0, 0)));
    static readonly Brush TileSelect = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x4A)));
    static readonly Brush TileAdjust = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xBF)));
    static readonly Brush BarBack = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x42)));
    static readonly Brush BarFill = Freeze(new SolidColorBrush(Color.FromRgb(0x5C, 0xC8, 0xFF)));
    static readonly Brush TextMain = Freeze(Brushes.White);
    static readonly Brush TextDim = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xA6)));

    static Brush Freeze(Brush b) { b.Freeze(); return b; }

    readonly StackPanel _tiles = new() { Orientation = Orientation.Horizontal };
    readonly TextBlock _hint = new() { Foreground = TextDim, FontSize = 12, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };

    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = new FontFamily("Segoe UI");

        var stack = new StackPanel();
        stack.Children.Add(_tiles);
        stack.Children.Add(_hint);
        Content = new Border
        {
            Background = Panel,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Margin = new Thickness(0),
            Child = stack,
        };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
        };
    }

    public void Render(List<Entry> entries, int selected, Mode mode)
    {
        _tiles.Children.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool sel = i == selected;
            float vol = e.Volume;

            var icon = e.Icon != null
                ? (UIElement)new Image { Source = e.Icon, Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Center }
                : new TextBlock { Text = e.IsMaster ? "\U0001F50A" : "\U0001F3B5", FontSize = 32, Foreground = TextMain, HorizontalAlignment = HorizontalAlignment.Center };

            var name = new TextBlock
            {
                Text = e.Name, Foreground = TextMain, FontSize = 13, FontWeight = sel ? FontWeights.SemiBold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0),
            };
            var pct = new TextBlock
            {
                Text = $"{Math.Round(vol * 100)}%", Foreground = sel ? TextMain : TextDim, FontSize = sel && mode == Mode.Adjust ? 20 : 15,
                FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 4),
            };
            var bar = new Grid { Height = 6, Margin = new Thickness(4, 0, 4, 0) };
            bar.Children.Add(new Border { Background = BarBack, CornerRadius = new CornerRadius(3) });
            bar.Children.Add(new Border { Background = BarFill, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0, Tag = vol });
            // width is set in Loaded because the bar's real width isn't known until layout
            var fill = (Border)bar.Children[1];
            bar.SizeChanged += (_, _) => fill.Width = bar.ActualWidth * Math.Clamp((float)fill.Tag, 0f, 1f);

            var col = new StackPanel();
            col.Children.Add(icon);
            col.Children.Add(name);
            col.Children.Add(pct);
            col.Children.Add(bar);

            _tiles.Children.Add(new Border
            {
                Width = 112, Margin = new Thickness(3), Padding = new Thickness(8, 12, 8, 12), CornerRadius = new CornerRadius(12),
                Background = !sel ? TileIdle : mode == Mode.Adjust ? TileAdjust : TileSelect,
                Child = col,
            });
        }
        _hint.Text = mode == Mode.Select
            ? "turn: choose   |   click: adjust   |   backspace: close"
            : "turn: volume   |   backspace: back   |   click: close";

        if (!IsVisible) Show();
        Reposition();
    }

    public void HideOverlay() => Hide();

    // Centre at the top of whichever monitor the foreground window (your game) is on.
    void Reposition()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        var mon = MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTOPRIMARY);
        int x = 0, y = 0;
        if (GetMonitorInfo(mon, ref mi))
        {
            x = mi.rcMonitor.left + ((mi.rcMonitor.right - mi.rcMonitor.left) - w) / 2;
            y = mi.rcMonitor.top + (int)(48 * dpi.DpiScaleY);
        }
        // Re-assert TOPMOST on every update: fullscreen windows can otherwise sit above us.
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}
