using System.Runtime.InteropServices;
using System.Text;

namespace TextEntryAssistant.Windows;

internal static class NativeMethods
{
    internal const uint InputKeyboard = 1;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const uint KeyEventUnicode = 0x0004;
    internal const int ErrorHotkeyAlreadyRegistered = 1409;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint numberOfInputs, [In] INPUT[] inputs, int sizeOfInput);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        internal uint Type;
        internal InputUnion Data;

        internal static INPUT Unicode(char value, bool keyUp) => new()
        {
            Type = InputKeyboard,
            Data = new InputUnion { Keyboard = new KEYBDINPUT { ScanCode = value, Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0) } }
        };

        internal static INPUT VirtualKey(ushort value, bool keyUp) => new()
        {
            Type = InputKeyboard,
            Data = new InputUnion { Keyboard = new KEYBDINPUT { VirtualKey = value, Flags = keyUp ? KeyEventKeyUp : 0 } }
        };
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct InputUnion
    {
        [FieldOffset(0)] internal KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }
}
