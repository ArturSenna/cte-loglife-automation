using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CteLogLife.Bsoft;

/// <summary>Virtual keys the automation presses.</summary>
public enum Key : ushort
{
    Tab = 0x09, Enter = 0x0D, Shift = 0x10, Control = 0x11, Alt = 0x12,
    Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28, Delete = 0x2E,
    F4 = 0x73, F10 = 0x79,
}

/// <summary>Mouse and keyboard events, sent the way the Python libraries sent them.</summary>
internal static class InputSender
{
    private static readonly int InputSize = Marshal.SizeOf<Native.INPUT>();

    public static void MoveTo(double x, double y) => Native.SetCursorPos((int)x, (int)y);

    public static void MouseClick(bool right = false)
    {
        Send(Mouse(right ? Native.MouseRightDown : Native.MouseLeftDown));
        Send(Mouse(right ? Native.MouseRightUp : Native.MouseLeftUp));
    }

    public static void KeyDown(Key key) => Send(Virtual((ushort)key, down: true, IsExtended(key)));

    public static void KeyUp(Key key) => Send(Virtual((ushort)key, down: false, IsExtended(key)));

    public static void Tap(Key key)
    {
        KeyDown(key);
        KeyUp(key);
    }

    /// <summary>
    /// A character as a key event: the plain key when the layout types it without modifiers (so
    /// shortcuts like Ctrl+V work), a Unicode event otherwise.
    /// </summary>
    public static void TapChar(char ch)
    {
        Send(Character(ch, down: true));
        Send(Character(ch, down: false));
    }

    /// <summary>
    /// A character typed as a person would: with Shift (or AltGr) held when the layout needs it.
    /// Characters the layout cannot type are sent as Unicode events.
    /// </summary>
    public static void TypeLikeAPerson(char ch)
    {
        var scan = Native.VkKeyScanW(ch);
        if (scan == -1)
        {
            Send(Unicode(ch, down: true));
            Send(Unicode(ch, down: false));
            return;
        }
        var key = (ushort)(scan & 0xFF);
        var modifiers = (scan >> 8) & 0xFF;
        var held = new List<Key>();
        if ((modifiers & 1) != 0) held.Add(Key.Shift);
        if ((modifiers & 2) != 0) held.Add(Key.Control);
        if ((modifiers & 4) != 0) held.Add(Key.Alt);
        foreach (var modifier in held) KeyDown(modifier);
        Send(Virtual(key, down: true, extended: false));
        Send(Virtual(key, down: false, extended: false));
        held.Reverse();
        foreach (var modifier in held) KeyUp(modifier);
    }

    /// <summary>Any virtual key by its code (used to repeat a key the operator pressed).</summary>
    public static void TapVirtual(int key)
    {
        // Page Up/Down, End, Home, the arrows, Insert and Delete live on the extended part of the keyboard.
        var extended = key is >= 0x21 and <= 0x28 or 0x2D or 0x2E;
        Send(Virtual((ushort)key, down: true, extended));
        Send(Virtual((ushort)key, down: false, extended));
    }

    private static bool IsExtended(Key key) => key is Key.Left or Key.Up or Key.Right or Key.Down or Key.Delete;

    private static Native.INPUT Character(char ch, bool down)
    {
        var scan = Native.VkKeyScanW(ch);
        var needsModifier = scan == -1 || ((scan >> 8) & 0xFF) != 0;
        return needsModifier ? Unicode(ch, down) : Virtual((ushort)(scan & 0xFF), down, extended: false);
    }

    private static Native.INPUT Virtual(ushort key, bool down, bool extended) => new()
    {
        type = Native.InputKeyboard,
        u = new Native.InputUnion
        {
            ki = new Native.KEYBDINPUT
            {
                wVk = key,
                wScan = (ushort)Native.MapVirtualKeyW(key, 0),
                dwFlags = (down ? 0 : Native.KeyUp) | (extended ? Native.KeyExtended : 0),
            },
        },
    };

    private static Native.INPUT Unicode(char ch, bool down) => new()
    {
        type = Native.InputKeyboard,
        u = new Native.InputUnion
        {
            ki = new Native.KEYBDINPUT { wScan = ch, dwFlags = Native.KeyUnicode | (down ? 0 : Native.KeyUp) },
        },
    };

    private static Native.INPUT Mouse(uint flags) => new()
    {
        type = Native.InputMouse,
        u = new Native.InputUnion { mi = new Native.MOUSEINPUT { dwFlags = flags } },
    };

    private static void Send(Native.INPUT input)
    {
        if (Native.SendInput(1, [input], InputSize) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "O Windows recusou o envio de teclado/mouse (outra janela com privilégio de administrador em foco?).");
    }
}
