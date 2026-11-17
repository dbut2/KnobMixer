using System;
using System.IO;

namespace KnobMixer;

/// <summary>Tiny error log at %LocalAppData%\KnobMixer\log.txt so failures are never silent.</summary>
public static class Log
{
    static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnobMixer", "log.txt");

    public static void Write(string msg)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch { }
    }
}
