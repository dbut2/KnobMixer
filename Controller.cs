using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace KnobMixer;

public enum Mode { Closed, Select, Adjust }

/// <summary>
/// The knob state machine.
///   Closed --click--> Select --click--> Adjust --click--> Closed
///   Adjust --Backspace--> Select,  Select --Backspace--> Closed,  Esc closes from anywhere.
/// While Closed, volume keys pass straight through, so Windows handles master volume itself.
/// </summary>
public sealed class Controller
{
    const int VK_BACK = 0x08, VK_ESCAPE = 0x1B, VK_MUTE = 0xAD, VK_DOWN = 0xAE, VK_UP = 0xAF;
    const float Step = 0.02f;

    readonly Dispatcher _ui;
    readonly AudioModel _audio = new();
    readonly OverlayWindow _overlay;
    readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly HashSet<int> _eaten = new(); // keys whose press we swallowed, so we also swallow their release

    // _mode is updated immediately inside the hook (so swallow decisions are right);
    // the slow work (audio calls, drawing) is queued to the UI thread, in order.
    Mode _mode = Mode.Closed;
    List<Entry> _entries = new();
    int _index;

    public Controller(Dispatcher ui, OverlayWindow overlay)
    {
        _ui = ui;
        _overlay = overlay;
        _idle.Tick += (_, _) => { if (_mode != Mode.Closed) { _mode = Mode.Closed; Queue(() => Apply(Mode.Closed)); } else _idle.Stop(); };
    }

    void Queue(Action a) => _ui.BeginInvoke(DispatcherPriority.Send, () =>
    {
        try { a(); } catch (Exception e) { Log.Write(e.ToString()); }
    });

    /// <summary>Called from the keyboard hook. Returns true to swallow the key.</summary>
    public bool OnKey(int vk, bool down)
    {
        if (!down) return _eaten.Remove(vk);

        bool repeat = _eaten.Contains(vk);

        if (_mode == Mode.Closed)
        {
            if (vk != VK_MUTE) return false; // knob turned while closed: let Windows change global volume
            _mode = Mode.Select;
            _eaten.Add(vk);
            Queue(Open);
            return true;
        }

        Mode m = _mode;
        switch (vk)
        {
            case VK_UP: Queue(() => Turn(+1, m)); break;
            case VK_DOWN: Queue(() => Turn(-1, m)); break;
            case VK_MUTE:
                if (!repeat) { _mode = m == Mode.Select ? Mode.Adjust : Mode.Closed; var n = _mode; Queue(() => Apply(n)); }
                break;
            case VK_BACK:
                if (!repeat) { _mode = m == Mode.Adjust ? Mode.Select : Mode.Closed; var n = _mode; Queue(() => Apply(n)); }
                break;
            case VK_ESCAPE:
                _mode = Mode.Closed; Queue(() => Apply(Mode.Closed)); break;
            default: return false;
        }
        _eaten.Add(vk);
        return true;
    }

    void Open()
    {
        _entries = _audio.Refresh();
        _index = 0;
        Apply(Mode.Select);
    }

    void Turn(int dir, Mode m)
    {
        if (_entries.Count == 0) return;
        if (m == Mode.Select) _index = (_index + dir + _entries.Count) % _entries.Count;
        else if (m == Mode.Adjust) _entries[_index].Nudge(dir * Step);
        Apply(m);
    }

    void Apply(Mode m)
    {
        _idle.Stop();
        if (m == Mode.Closed)
        {
            _overlay.HideOverlay();
            _audio.Dispose();
            return;
        }
        _overlay.Render(_entries, _index, m);
        _idle.Start(); // restart the 5s auto-close countdown
    }
}
