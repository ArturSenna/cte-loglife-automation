using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CteLogLife.App;

/// <summary>The Win32 bits WPF does not expose: overlay window styles and global hotkeys.</summary>
internal static class Interop
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
    private const uint WdaNone = 0x0, WdaExcludeFromCapture = 0x11;

    public const int WmHotkey = 0x0312;
    public const int WmMouseActivate = 0x0021;
    public static readonly IntPtr MaActivate = new(1), MaNoActivate = new(3);
    public const uint ModAlt = 0x1, ModControl = 0x2, ModNoRepeat = 0x4000;
    public const uint VkPause = 0x13, VkP = 0x50, VkQ = 0x51;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr window, int id);

    private static IntPtr Handle(Window window) => new WindowInteropHelper(window).Handle;

    private static void SetExStyle(Window window, long flags, bool on)
    {
        var handle = Handle(window);
        if (handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        style = on ? style | flags : style & ~flags;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    /// <summary>Never takes the keyboard focus (it would take it from Bsoft) and stays out of Alt+Tab.</summary>
    public static void MakeOverlay(Window window) => SetExStyle(window, WsExNoActivate | WsExToolWindow, true);

    /// <summary>When off, a click can give the window the keyboard (needed to type in it).</summary>
    public static void SetNoActivate(Window window, bool on) => SetExStyle(window, WsExNoActivate, on);

    /// <summary>The window that has the keyboard right now.</summary>
    public static IntPtr ForegroundWindow() => GetForegroundWindow();

    /// <summary>Whether a window belongs to this program.</summary>
    public static bool IsOurs(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out var process);
        return process == (uint)Environment.ProcessId;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint thread, uint attachTo, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }

    /// <summary>Where the mouse pointer is, in screen pixels.</summary>
    public static Point CursorPosition() => GetCursorPos(out var point) ? new Point(point.X, point.Y) : new Point(-1, -1);

    /// <summary>
    /// Makes the window the one that gets the keyboard. Windows only lets a program do this in a few
    /// situations; when the plain request is refused, it is repeated while sharing the input state of
    /// the program that has the keyboard, which is the documented way around it.
    /// </summary>
    public static bool TakeForeground(Window window) => GiveForeground(Handle(window));

    /// <summary>The same for any window, used to hand the keyboard back to the one that had it.</summary>
    public static bool GiveForeground(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !IsWindow(handle)) return false;
        if (GetForegroundWindow() == handle) return true;
        SetForegroundWindow(handle);
        if (GetForegroundWindow() == handle) return true;

        var other = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var mine = GetCurrentThreadId();
        if (other == 0 || other == mine || !AttachThreadInput(mine, other, true)) return false;
        try
        {
            SetForegroundWindow(handle);
        }
        finally
        {
            AttachThreadInput(mine, other, false);
        }
        return GetForegroundWindow() == handle;
    }

    /// <summary>Whether this window is the one Windows sends the keyboard to.</summary>
    public static bool IsForeground(Window window)
    {
        var handle = Handle(window);
        return handle != IntPtr.Zero && GetForegroundWindow() == handle;
    }

    /// <summary>When on, mouse clicks go through the window to whatever is under it.</summary>
    public static void SetClickThrough(Window window, bool on) => SetExStyle(window, WsExTransparent, on);

    /// <summary>Keeps the window out of screenshots, including the ones the bot takes of Bsoft.</summary>
    public static bool ExcludeFromCapture(Window window, bool on) => ExcludeFromCapture(Handle(window), on);

    public static bool ExcludeFromCapture(IntPtr handle, bool on) =>
        handle != IntPtr.Zero && SetWindowDisplayAffinity(handle, on ? WdaExcludeFromCapture : WdaNone);
}

/// <summary>System-wide shortcuts that work while Bsoft has the keyboard.</summary>
internal sealed class GlobalHotkeys : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = [];

    public GlobalHotkeys(Window window)
    {
        _source = (HwndSource)PresentationSource.FromVisual(window)!;
        _source.AddHook(Hook);
    }

    /// <summary>Returns false when another program already owns the shortcut.</summary>
    public bool Add(uint modifiers, uint key, Action action)
    {
        var id = 0x4C00 + _actions.Count;
        if (!Interop.RegisterHotKey(_source.Handle, id, modifiers | Interop.ModNoRepeat, key)) return false;
        _actions[id] = action;
        return true;
    }

    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == Interop.WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) Interop.UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
        _source.RemoveHook(Hook);
    }
}
