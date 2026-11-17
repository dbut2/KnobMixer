using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KnobMixer;

/// <summary>Global low-level keyboard hook. Works regardless of which window has focus.</summary>
public sealed class KeyboardHook : IDisposable
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);

    readonly HookProc _proc; // kept in a field so the GC can't collect it while the hook is live
    readonly Func<int, bool, bool> _onKey;
    IntPtr _hook;

    /// <param name="onKey">(virtualKey, isDown) -> true to swallow the key so no other app sees it.</param>
    public KeyboardHook(Func<int, bool, bool> onKey)
    {
        _onKey = onKey;
        _proc = Callback;
        using var cur = Process.GetCurrentProcess();
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(cur.MainModule!.ModuleName), 0);
        if (_hook == IntPtr.Zero) throw new InvalidOperationException("Could not install keyboard hook: " + Marshal.GetLastWin32Error());
    }

    IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            try
            {
                var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;
                bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool up = msg == WM_KEYUP || msg == WM_SYSKEYUP;
                if ((down || up) && _onKey((int)k.vkCode, down)) return (IntPtr)1;
            }
            catch (Exception e) { Log.Write("hook: " + e); } // never let an exception escape a hook callback
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}
