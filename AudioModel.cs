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

    public List<Entry> Refresh()
    {
        Dispose();
        _enum = new MMDeviceEnumerator();
        _dev = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        var list = new List<Entry>
        {
            new Entry { Name = "Master", IsMaster = true, Endpoint = _dev.AudioEndpointVolume }
        };

        var byPid = new Dictionary<uint, Entry>();
        var sessions = _dev.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            if (s.State == AudioSessionState.AudioSessionStateExpired || s.IsSystemSoundsSession) continue;
            uint pid = s.GetProcessID;
            if (!byPid.TryGetValue(pid, out var entry))
            {
                var (name, icon) = Describe(pid, s.DisplayName);
                entry = new Entry { Name = name, Icon = icon };
                byPid[pid] = entry;
                list.Add(entry);
            }
            entry.Sessions.Add(s);
        }
        return list;
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
        _dev?.Dispose();
        _enum?.Dispose();
        _dev = null;
        _enum = null;
    }
}
