using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KnobMixer;

/// <summary>Vector icons on a 24x24 grid, so they stay crisp at any DPI (no emoji, no bitmaps).</summary>
public static class Icons
{
    static readonly Geometry SpeakerBody = Make("M3 9v6h4l5 5V4L7 9H3z");
    static readonly Geometry WaveLow     = Make("M16.5 12A4.5 4.5 0 0 0 14 8v8a4.5 4.5 0 0 0 2.5-4z");
    static readonly Geometry WaveHigh    = Make("M14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z");
    static readonly Geometry Cross       = Make("M16 9.5l5 5M21 9.5l-5 5");
    static readonly Geometry Bars        = Make("M4 10h2.4v4H4z M8.4 6h2.4v12H8.4z M12.8 3h2.4v18h-2.4z M17.2 8h2.4v8h-2.4z");

    static Geometry Make(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    /// <summary>Speaker whose waves reflect the level; a cross when muted.</summary>
    public static UIElement Speaker(float volume, bool muted, Brush brush, double size)
    {
        var c = new Canvas { Width = 24, Height = 24 };
        c.Children.Add(new Path { Data = SpeakerBody, Fill = brush });
        if (muted)
        {
            c.Children.Add(new Path
            {
                Data = Cross, Stroke = brush, StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            });
        }
        else
        {
            if (volume > 0.01f) c.Children.Add(new Path { Data = WaveLow, Fill = brush });
            if (volume > 0.5f) c.Children.Add(new Path { Data = WaveHigh, Fill = brush });
        }
        return new Viewbox { Width = size, Height = size, Child = c };
    }

    /// <summary>Generic "audio app" glyph for apps whose own icon can't be read.</summary>
    public static UIElement App(Brush brush, double size)
    {
        var c = new Canvas { Width = 24, Height = 24 };
        c.Children.Add(new Path { Data = Bars, Fill = brush });
        return new Viewbox { Width = size, Height = size, Child = c };
    }
}
