using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace KnobMixer;

/// <summary>One thing the knob can control: the master volume, or one app (all of its audio sessions).</summary>
public sealed class Entry
{
    public string Name { get; init; } = "";
    public ImageSource? Icon { get; init; }
    public bool IsMaster { get; init; }
    internal AudioEndpointVolume? Endpoint { get; init; }
    internal List<AudioSessionControl> Sessions { get; } = new();

    public float Volume
    {
        get
        {
            if (IsMaster) return Endpoint!.MasterVolumeLevelScalar;
            return Sessions.Count == 0 ? 0f : Sessions.Max(s => s.SimpleAudioVolume.Volume);
        }
    }

    public bool Muted => IsMaster && Endpoint!.Mute;

    public void Nudge(float delta)
    {
        float v = Math.Clamp(Volume + delta, 0f, 1f);
        if (IsMaster)
        {
            if (delta > 0 && Endpoint!.Mute) Endpoint.Mute = false; // raising volume unmutes, like Windows does
            Endpoint!.MasterVolumeLevelScalar = v;
        }
        else
        {
            foreach (var s in Sessions) s.SimpleAudioVolume.Volume = v;
        }
    }
}

/// <summary>Reads the default playback device and its per-app audio sessions.</summary>
public sealed class AudioModel : IDisposable
{
    MMDeviceEnumerator? _enum;
    MMDevice? _dev;
    readonly List<MMDevice> _devices = new();

    public List<Entry> Refresh()
    {
        Dispose();
        _enum = new MMDeviceEnumerator();
        _dev = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        var list = new List<Entry>
        {
            new Entry { Name = "Master", IsMaster = true, Endpoint = _dev.AudioEndpointVolume }
        };

        // Apps can be routed to any output device (e.g. a "Game" and a "Chat" headset device),
        // so gather sessions from every active output, not just the default one. An app playing
        // on two devices becomes one entry whose slider moves both.
        var byApp = new Dictionary<string, Entry>();
        foreach (var dev in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            _devices.Add(dev);
            var sessions = dev.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                if (s.State == AudioSessionState.AudioSessionStateExpired || s.IsSystemSoundsSession) continue;
                uint pid = s.GetProcessID;
                // One tile per app, not per process: Discord, Chrome etc. run several processes
                // from the same exe, each with its own audio session.
                string key = AppKey(pid);
                if (!byApp.TryGetValue(key, out var entry))
                {
                    var (name, icon) = Describe(pid, s.DisplayName);
                    entry = new Entry { Name = name, Icon = icon };
                    byApp[key] = entry;
                    list.Add(entry);
                }
                entry.Sessions.Add(s);
            }
        }
        return list;
    }

    /// <summary>Executable path identifies the app; falls back to the pid if the path can't be read.</summary>
    static string AppKey(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            var path = p.MainModule?.FileName;
            if (!string.IsNullOrEmpty(path)) return path.ToLowerInvariant();
        }
        catch { /* protected or already exited */ }
        return "pid:" + pid;
    }

    static (string, ImageSource?) Describe(uint pid, string sessionName)
    {
        string name = "";
        ImageSource? icon = null;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            string? path = null;
            try { path = p.MainModule?.FileName; } catch { /* protected/elevated process */ }
            if (path != null)
            {
                try { name = FileVersionInfo.GetVersionInfo(path).FileDescription ?? ""; } catch { }
                try
                {
                    using var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (ico != null)
                    {
                        var bs = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, System.Windows.Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        icon = bs;
                    }
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(sessionName) && !sessionName.StartsWith("@"))
                name = sessionName;
            if (string.IsNullOrWhiteSpace(name)) name = p.ProcessName;
        }
        catch { if (string.IsNullOrWhiteSpace(name)) name = string.IsNullOrWhiteSpace(sessionName) ? $"PID {pid}" : sessionName; }
        return (name, icon);
    }

    public void Dispose()
    {
        foreach (var d in _devices) d.Dispose();
        _devices.Clear();
        _dev?.Dispose();
        _enum?.Dispose();
        _dev = null;
        _enum = null;
    }
}
