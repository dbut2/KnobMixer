using System.Windows.Media;

namespace KnobMixer;

/// <summary>
/// Design tokens for the overlay. Flat dark theme with one teal accent.
/// Every colour used by the UI comes from here; nothing is hard-coded in layout code.
/// All brushes are frozen (cheaper to render, safe to share).
/// </summary>
public static class Theme
{
    // Surfaces
    public static readonly Brush Surface        = Solid(0xF2, 0x16, 0x18, 0x1D); // panel
    public static readonly Brush SurfaceRaised  = Solid(0xFF, 0x26, 0x2A, 0x33); // selected tile, keycaps
    public static readonly Brush Hairline       = Solid(0x1F, 0xFF, 0xFF, 0xFF); // 12% white
    public static readonly Brush HairlineStrong = Solid(0xFF, 0x3A, 0x3F, 0x4B);

    // Text (both clear 4.5:1 against Surface)
    public static readonly Brush TextPrimary    = Solid(0xFF, 0xEC, 0xEE, 0xF1);
    public static readonly Brush TextSecondary  = Solid(0xFF, 0x9B, 0xA1, 0xAB);

    // Accent (teal) and its tint
    public static readonly Brush Accent         = Solid(0xFF, 0x2D, 0xD4, 0xBF);
    public static readonly Brush AccentTint     = Solid(0x1F, 0x2D, 0xD4, 0xBF); // 12%

    // Volume bar
    public static readonly Brush Track          = Solid(0xFF, 0x2B, 0x2F, 0x38);
    public static readonly Brush FillIdle       = Solid(0xFF, 0x7C, 0x84, 0x91);

    // Muted state
    public static readonly Brush Muted          = Solid(0xFF, 0xF8, 0x71, 0x71);

    public static readonly Brush Transparent    = Solid(0x00, 0, 0, 0);

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");

    // Metrics
    public const double PanelRadius = 14, TileRadius = 10, TileWidth = 108;

    static Brush Solid(byte a, byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }
}
