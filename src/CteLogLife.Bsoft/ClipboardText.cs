using System.Runtime.InteropServices;
using System.Text;

namespace CteLogLife.Bsoft;

public sealed class ClipboardBusyException(string message) : Exception(message);

/// <summary>
/// Text clipboard access that waits for the clipboard instead of failing. Only one program can have it
/// open at a time, and clipboard history, sharing tools and remote-desktop clients open it on every change.
/// </summary>
public static class ClipboardText
{
    /// <summary>How long a call keeps trying while another program holds the clipboard.</summary>
    public static TimeSpan RetryFor { get; set; } = TimeSpan.FromSeconds(5);

    public static void Set(string text) => Use(RetryFor, () =>
    {
        Native.EmptyClipboard();
        var bytes = Encoding.Unicode.GetBytes(text + "\0");
        var memory = Native.GlobalAlloc(Native.GmemMoveable, (UIntPtr)bytes.Length);
        if (memory == IntPtr.Zero) throw new OutOfMemoryException();
        var pointer = Native.GlobalLock(memory);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        Native.GlobalUnlock(memory);
        if (Native.SetClipboardData(Native.CfUnicodeText, memory) == IntPtr.Zero)
        {
            Native.GlobalFree(memory);
            throw new ClipboardBusyException("Não foi possível gravar na área de transferência.");
        }
        return true;
    });

    /// <summary>Empties the clipboard, so an old value can never be mistaken for a new copy.</summary>
    public static void Clear() => Use(RetryFor, () => Native.EmptyClipboard());

    /// <summary>The clipboard text, or "" when it holds no text.</summary>
    public static string Get() => Use(RetryFor, ReadText);

    /// <summary>The clipboard text, or null when another program has the clipboard right now.</summary>
    public static string? TryGet()
    {
        try
        {
            return Use(TimeSpan.Zero, ReadText);
        }
        catch (ClipboardBusyException)
        {
            return null;
        }
    }

    private static string ReadText()
    {
        if (!Native.IsClipboardFormatAvailable(Native.CfUnicodeText)) return "";
        var memory = Native.GetClipboardData(Native.CfUnicodeText);
        var pointer = memory == IntPtr.Zero ? IntPtr.Zero : Native.GlobalLock(memory);
        if (pointer == IntPtr.Zero) return "";
        try
        {
            return Marshal.PtrToStringUni(pointer) ?? "";
        }
        finally
        {
            Native.GlobalUnlock(memory);
        }
    }

    private static T Use<T>(TimeSpan retryFor, Func<T> whileOpen)
    {
        // An owner window makes SetClipboardData reliable; a message-only one is never visible.
        var owner = Native.CreateWindowExW(0, "STATIC", null, 0, 0, 0, 0, 0, Native.HwndMessage, IntPtr.Zero,
            IntPtr.Zero, IntPtr.Zero);
        try
        {
            var deadline = DateTime.UtcNow + retryFor;
            while (!Native.OpenClipboard(owner))
            {
                if (DateTime.UtcNow >= deadline)
                    throw new ClipboardBusyException(
                        "A área de transferência está em uso por outro programa e não foi liberada a tempo.");
                Thread.Sleep(50);
            }
            try
            {
                return whileOpen();
            }
            finally
            {
                Native.CloseClipboard();
            }
        }
        finally
        {
            if (owner != IntPtr.Zero) Native.DestroyWindow(owner);
        }
    }
}
