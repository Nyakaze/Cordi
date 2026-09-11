using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Cordi.Services.Chatbox;

internal static class GameWindowAlert
{
    private const string GameWindowClass = "FFXIVGAME";

    private const uint FlashStop = 0x00000000;
    private const uint FlashCaption = 0x00000001;
    private const uint FlashTray = 0x00000002;
    private const uint FlashTimerNoForeground = 0x0000000C;

    private const int ShowRestore = 9;

    private const uint GetForegroundLockTimeout = 0x2000;
    private const uint SetForegroundLockTimeout = 0x2001;

    private const uint GetWindowOwner = 4;

    private static readonly EnumWindowsProc EnumCallback = OnEnumWindow;

    private static IntPtr _handle;
    private static IntPtr _candidate;
    private static uint _processId;

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool join);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(IntPtr window, StringBuilder buffer, int capacity);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint update);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint update);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    public static bool IsForeground()
    {
        var window = Handle();

        return window != IntPtr.Zero && GetForegroundWindow() == window;
    }

    public static void Flash()
    {
        var window = Handle();
        if (window == IntPtr.Zero) return;

        var info = new FlashWindowInfo
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Window = window,
            Flags = FlashTray | FlashCaption | FlashTimerNoForeground,
            Count = uint.MaxValue,
            Timeout = 0,
        };

        FlashWindowEx(ref info);
    }

    public static void StopFlashing()
    {
        var window = Handle();
        if (window == IntPtr.Zero) return;

        var info = new FlashWindowInfo
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Window = window,
            Flags = FlashStop,
            Count = 0,
            Timeout = 0,
        };

        FlashWindowEx(ref info);
    }

    public static void BringToFront()
    {
        var window = Handle();
        if (window == IntPtr.Zero) return;

        if (IsIconic(window)) ShowWindow(window, ShowRestore);

        var foreground = GetForegroundWindow();
        if (foreground == window) return;

        var self = GetCurrentThreadId();
        var owner = foreground == IntPtr.Zero ? 0u : GetWindowThreadProcessId(foreground, out _);
        var attached = owner != 0 && owner != self && AttachThreadInput(self, owner, true);

        var timeout = ReadForegroundLockTimeout();
        WriteForegroundLockTimeout(0);

        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            WriteForegroundLockTimeout(timeout);

            if (attached) AttachThreadInput(self, owner, false);
        }
    }

    private static uint ReadForegroundLockTimeout()
    {
        uint value = 0;

        return SystemParametersInfo(GetForegroundLockTimeout, 0, ref value, 0) ? value : 0;
    }

    private static void WriteForegroundLockTimeout(uint value) =>
        SystemParametersInfo(SetForegroundLockTimeout, 0, new IntPtr(value), 0);

    private static IntPtr Handle()
    {
        if (_handle != IntPtr.Zero && IsWindow(_handle)) return _handle;

        _handle = Resolve();
        return _handle;
    }

    private static IntPtr Resolve()
    {
        _candidate = IntPtr.Zero;
        _processId = GetCurrentProcessId();

        try
        {
            EnumWindows(EnumCallback, IntPtr.Zero);
        }
        catch (Exception)
        {
            _candidate = IntPtr.Zero;
        }

        if (_candidate != IntPtr.Zero) return _candidate;

        try
        {
            using var process = Process.GetCurrentProcess();
            return process.MainWindowHandle;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    private static bool OnEnumWindow(IntPtr window, IntPtr param)
    {
        GetWindowThreadProcessId(window, out var owner);
        if (owner != _processId) return true;

        if (!IsWindowVisible(window)) return true;
        if (GetWindow(window, GetWindowOwner) != IntPtr.Zero) return true;

        if (_candidate == IntPtr.Zero) _candidate = window;

        var buffer = new StringBuilder(64);
        if (GetClassName(window, buffer, buffer.Capacity) == 0) return true;

        if (!string.Equals(buffer.ToString(), GameWindowClass, StringComparison.Ordinal)) return true;

        _candidate = window;
        return false;
    }
}
