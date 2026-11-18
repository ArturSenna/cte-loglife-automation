using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CteLogLife.Bsoft;

/// <summary>
/// Everything the bot does to the desktop apart from looking at it: mouse, keyboard, clipboard, time
/// and which window has the keyboard. The real one sends real events; tests use one that records them
/// and runs on its own clock.
/// </summary>
public interface IDesktop
{
    void MoveTo(double x, double y);
    void MouseClick(bool right = false);
    void KeyDown(Key key);
    void KeyUp(Key key);
    void Tap(Key key);

    /// <summary>A character as a key event (the plain key when the layout has it, so Ctrl+V works).</summary>
    void TapChar(char ch);

    /// <summary>A character typed as a person would, with Shift held when the layout needs it.</summary>
    void TypeLikeAPerson(char ch);

    void SetClipboard(string text);
    void ClearClipboard();

    /// <summary>The clipboard text ("" when it holds none), or null when another program has it open.</summary>
    string? TryGetClipboard();

    /// <summary>Milliseconds on a clock that only moves forward.</summary>
    long Now { get; }

    void Sleep(int ms);

    /// <summary>The window that has the keyboard.</summary>
    IntPtr Foreground();

    /// <summary>The program that owns a window (0 when unknown).</summary>
    uint ProcessOf(IntPtr window);

    bool GiveForeground(IntPtr window);

    /// <summary>
    /// Where on the screen the window with the keyboard is, when it belongs to another program
    /// (Bsoft while the bot works). Null when there is none or it is a window of this program.
    /// </summary>
    ScreenBox? ForegroundArea();

    /// <summary>The text of the control that has the keyboard focus, when Windows can read it.</summary>
    string? FocusedText();
}

/// <summary>
/// The real mouse, keyboard, clipboard and clock. While it exists Windows is asked for a 1 ms timer:
/// with the default one every short wait takes up to 16 ms longer than asked.
/// </summary>
public sealed class RealDesktop : IDesktop, IDisposable
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _fineTimer = Native.timeBeginPeriod(1) == 0;

    public void Dispose()
    {
        if (!_fineTimer) return;
        _fineTimer = false;
        Native.timeEndPeriod(1);
    }

    public void MoveTo(double x, double y) => InputSender.MoveTo(x, y);
    public void MouseClick(bool right = false) => InputSender.MouseClick(right);
    public void KeyDown(Key key) => InputSender.KeyDown(key);
    public void KeyUp(Key key) => InputSender.KeyUp(key);
    public void Tap(Key key) => InputSender.Tap(key);
    public void TapChar(char ch) => InputSender.TapChar(ch);
    public void TypeLikeAPerson(char ch) => InputSender.TypeLikeAPerson(ch);

    public void SetClipboard(string text) => ClipboardText.Set(text);
    public void ClearClipboard() => ClipboardText.Clear();
    public string? TryGetClipboard() => ClipboardText.TryGet();

    public long Now => _clock.ElapsedMilliseconds;
    public void Sleep(int ms) => Thread.Sleep(ms);

    public IntPtr Foreground() => Native.GetForegroundWindow();

    public uint ProcessOf(IntPtr window)
    {
        if (window == IntPtr.Zero) return 0;
        Native.GetWindowThreadProcessId(window, out var process);
        return process;
    }

    public bool GiveForeground(IntPtr window) => Native.IsWindow(window) && Native.SetForegroundWindow(window);

    public ScreenBox? ForegroundArea()
    {
        var window = Native.GetForegroundWindow();
        if (window == IntPtr.Zero || (ProcessOf(window) == (uint)Environment.ProcessId && !WatchOwnWindows)) return null;
        if (!Native.GetWindowRect(window, out var area)) return null;
        return new ScreenBox(area.Left, area.Top, area.Right - area.Left, area.Bottom - area.Top);
    }

    /// <summary>Self-test only: treat a window of this program as the one the bot works in.</summary>
    public bool WatchOwnWindows { get; init; }

    public string? FocusedText()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return null;
        var thread = Native.GetWindowThreadProcessId(foreground, out _);
        var info = new Native.GUITHREADINFO { cbSize = Marshal.SizeOf<Native.GUITHREADINFO>() };
        if (!Native.GetGUIThreadInfo(thread, ref info) || info.hwndFocus == IntPtr.Zero) return null;

        if (Native.SendMessageTimeoutW(info.hwndFocus, Native.WmGetTextLength, IntPtr.Zero, IntPtr.Zero,
                Native.SmtoAbortIfHung, 500, out var length) == IntPtr.Zero) return null;
        var size = (int)length;
        if (size is <= 0 or > 64) return null;

        var buffer = new char[size + 1];
        if (Native.SendMessageTimeoutText(info.hwndFocus, Native.WmGetText, buffer.Length, buffer,
                Native.SmtoAbortIfHung, 500, out var copied) == IntPtr.Zero) return null;
        return new string(buffer, 0, Math.Min((int)copied, size)).Trim();
    }
}
