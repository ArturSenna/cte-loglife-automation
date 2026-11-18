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

    public static bool BringToFront(IntPtr window) => IsWindow(window) && SetForegroundWindow(window);

    /// <summary>When on, mouse clicks go through the window to whatever is under it.</summary>
    public static void SetClickThrough(Window window, bool on) => SetExStyle(window, WsExTransparent, on);

    /// <summary>Keeps the window out of screenshots, including the ones the bot takes of Bsoft.</summary>
    public static bool ExcludeFromCapture(Window window, bool on)
    {
        var handle = Handle(window);
        return handle != IntPtr.Zero && SetWindowDisplayAffinity(handle, on ? WdaExcludeFromCapture : WdaNone);
    }
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
