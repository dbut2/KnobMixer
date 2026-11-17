using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KnobMixer;

/// <summary>
/// Borderless always-on-top panel that never takes focus (so a game keeps running underneath)
/// and ignores the mouse. Colours and metrics come from <see cref="Theme"/>.
/// </summary>
public sealed class OverlayWindow : Window
{
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
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

    static readonly Duration FadeIn = new(TimeSpan.FromMilliseconds(150));
    static readonly Duration FadeOut = new(TimeSpan.FromMilliseconds(110));

    readonly StackPanel _tiles = new() { Orientation = Orientation.Horizontal };
    readonly StackPanel _hints = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    readonly TranslateTransform _slide = new();
    bool _shown;   // true from fade-in start until a hide begins

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
        FontFamily = Theme.Font;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        UseLayoutRounding = true;
        Opacity = 0;

        var divider = new Border { Height = 1, Background = Theme.Hairline, Margin = new Thickness(4, 10, 4, 10) };
        var stack = new StackPanel();
        stack.Children.Add(_tiles);
        stack.Children.Add(divider);
        stack.Children.Add(_hints);

        Content = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Theme.PanelRadius),
            Padding = new Thickness(10, 10, 10, 12),
            Child = stack,
            RenderTransform = _slide,
        };

        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
        };
    }

    // ---- building blocks ---------------------------------------------------------------------

    static TextBlock Label(string text, double size, Brush brush, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, Foreground = brush, FontWeight = weight ?? FontWeights.Normal,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    UIElement Tile(Entry e, bool selected, Mode mode)
    {
        bool adjusting = selected && mode == Mode.Adjust;
        float vol = e.Volume;
        Brush fg = selected ? Theme.TextPrimary : Theme.TextSecondary;
        Brush barFill = adjusting ? Theme.Accent : selected ? Theme.TextPrimary : Theme.FillIdle;

        UIElement icon = e.IsMaster
            ? Icons.Speaker(vol, e.Muted, e.Muted ? Theme.Muted : fg, 28)
            : e.Icon != null
                ? new System.Windows.Controls.Image { Source = e.Icon, Width = 28, Height = 28, Opacity = selected ? 1 : 0.75 }
                : Icons.App(fg, 28);
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);

        var iconSlot = new Border { Height = 32, Child = icon, HorizontalAlignment = HorizontalAlignment.Center };

        var name = Label(e.Name, 12, fg, selected ? FontWeights.SemiBold : FontWeights.Normal);
        name.TextAlignment = TextAlignment.Center;
        name.Margin = new Thickness(0, 8, 0, 0);

        string pctText = e.Muted ? "Muted" : $"{Math.Round(vol * 100)}%";
        var pct = Label(pctText, adjusting ? 20 : 16, e.Muted ? Theme.Muted : adjusting ? Theme.Accent : fg, FontWeights.SemiBold);
        pct.TextAlignment = TextAlignment.Center;
        pct.Margin = new Thickness(0, 2, 0, 8);
        Typography.SetNumeralAlignment(pct, FontNumeralAlignment.Tabular);

        var track = new Grid { Height = 4, Margin = new Thickness(2, 0, 2, 0) };
        track.Children.Add(new Border { Background = Theme.Track, CornerRadius = new CornerRadius(2) });
        var fill = new Border { Background = barFill, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
        track.Children.Add(fill);
        float clamped = Math.Clamp(vol, 0f, 1f);
        track.SizeChanged += (_, _) => fill.Width = track.ActualWidth * clamped; // width unknown until layout

        var col = new StackPanel();
        col.Children.Add(iconSlot);
        col.Children.Add(name);
        col.Children.Add(pct);
        col.Children.Add(track);

        return new Border
        {
            Width = Theme.TileWidth,
            Margin = new Thickness(2),
            Padding = new Thickness(10, 12, 10, 12),
            CornerRadius = new CornerRadius(Theme.TileRadius),
            // Same border thickness in every state so tiles never shift when selection changes.
            BorderThickness = new Thickness(1.5),
            BorderBrush = adjusting ? Theme.Accent : selected ? Theme.HairlineStrong : Theme.Transparent,
            Background = adjusting ? Theme.AccentTint : selected ? Theme.SurfaceRaised : Theme.Transparent,
            Child = col,
        };
    }

    static UIElement Hint(string key, string action)
    {
        var cap = new Border
        {
            Background = Theme.SurfaceRaised, BorderBrush = Theme.HairlineStrong, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 2),
            Child = Label(key, 11, Theme.TextPrimary, FontWeights.SemiBold),
        };
        var text = Label(action, 11, Theme.TextSecondary);
        text.Margin = new Thickness(6, 0, 0, 0);
        text.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0) };
        row.Children.Add(cap);
        row.Children.Add(text);
        return row;
    }

    // ---- public API ---------------------------------------------------------------------------

    public void Render(List<Entry> entries, int selected, Mode mode)
    {
        _tiles.Children.Clear();
        for (int i = 0; i < entries.Count; i++) _tiles.Children.Add(Tile(entries[i], i == selected, mode));

        _hints.Children.Clear();
        if (mode == Mode.Select)
        {
            _hints.Children.Add(Hint("Turn", "Choose"));
            _hints.Children.Add(Hint("Click", "Adjust"));
            _hints.Children.Add(Hint("Backspace", "Close"));
        }
        else
        {
            _hints.Children.Add(Hint("Turn", "Volume"));
            _hints.Children.Add(Hint("Backspace", "Back"));
            _hints.Children.Add(Hint("Click", "Done"));
        }

        if (!IsVisible) Show();
        Reposition();
        if (!_shown) { _shown = true; Animate(1, 0, FadeIn); }
    }

    public void HideOverlay()
    {
        if (!_shown) return;
        _shown = false;
        Animate(0, 4, FadeOut, onDone: () => { if (!_shown) Hide(); });
    }

    /// <summary>Fade (and a few px of slide) to the target. Skips animation if Windows has animations turned off.</summary>
    void Animate(double opacity, double offsetY, Duration d, Action? onDone = null)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = opacity;
            _slide.Y = 0;
            onDone?.Invoke();
            return;
        }
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(opacity, d) { EasingFunction = ease };
        if (onDone != null) fade.Completed += (_, _) => onDone();
        BeginAnimation(OpacityProperty, fade);
        _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, d) { EasingFunction = ease });
    }

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
