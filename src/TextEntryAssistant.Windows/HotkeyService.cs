using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace TextEntryAssistant.Windows;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

public sealed record HotkeyDefinition(HotkeyModifiers Modifiers, Key Key)
{
    public override string ToString() => $"{FormatModifiers(Modifiers)}{Key}";

    public static bool TryParse(string text, out HotkeyDefinition? definition)
    {
        definition = null;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        var modifiers = HotkeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Control,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" or "windows" => HotkeyModifiers.Windows,
                _ => (HotkeyModifiers)uint.MaxValue
            };
            if (modifiers == (HotkeyModifiers)uint.MaxValue) return false;
        }
        var keyName = parts[^1].ToLowerInvariant() switch
        {
            "esc" => nameof(Key.Escape),
            "return" => nameof(Key.Enter),
            "space" => nameof(Key.Space),
            _ => parts[^1]
        };
        if (!Enum.TryParse<Key>(keyName, true, out var key) || key == Key.None) return false;
        definition = new HotkeyDefinition(modifiers, key);
        return true;
    }

    private static string FormatModifiers(HotkeyModifiers modifiers)
    {
        var names = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) names.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) names.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) names.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) names.Add("Win");
        return names.Count == 0 ? string.Empty : string.Join('+', names) + "+";
    }
}

public sealed class HotkeyRegistrationException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, HotkeyDefinition> _registered = [];
    private HwndSource? _source;
    private int _nextId = 1;

    public event Action<HotkeyDefinition>? Pressed;

    public void Attach(Window window)
    {
        if (_source is not null) return;
        var handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("无法连接窗口消息源。");
        _source.AddHook(WndProc);
    }

    public void Replace(IReadOnlyDictionary<string, HotkeyDefinition> definitions)
    {
        if (_source is null) throw new InvalidOperationException("热键服务尚未连接窗口。");
        foreach (var id in _registered.Keys.ToArray())
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
        _nextId = 1;
        foreach (var definition in definitions.Values)
        {
            var id = _nextId++;
            if (!NativeMethods.RegisterHotKey(_source.Handle, id, (uint)definition.Modifiers, (uint)KeyInterop.VirtualKeyFromKey(definition.Key)))
            {
                var error = Marshal.GetLastWin32Error();
                var detail = error == NativeMethods.ErrorHotkeyAlreadyRegistered ? "该组合键已被其他程序占用。" : new Win32Exception(error).Message;
                throw new HotkeyRegistrationException($"无法注册全局快捷键 {definition}：{detail}");
            }
            _registered[id] = definition;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmHotkey = 0x0312;
        if (message == WmHotkey && _registered.TryGetValue(wParam.ToInt32(), out var definition))
        {
            Pressed?.Invoke(definition);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null) return;
        foreach (var id in _registered.Keys)
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
        _source.RemoveHook(WndProc);
        _source = null;
    }
}
