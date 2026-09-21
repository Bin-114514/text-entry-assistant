using System.ComponentModel;
using System.Runtime.InteropServices;
using TextEntryAssistant.Core;

namespace TextEntryAssistant.Windows;

public sealed class SendInputTextInjector : ITextInjector
{
    private const int Shift = 0x10;
    private const int Control = 0x11;
    private const int Alt = 0x12;
    private const int Windows = 0x5B;
    private const ushort Enter = 0x0D;
    private const ushort Tab = 0x09;

    public async Task SendTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        await WaitForModifiersReleasedAsync(cancellationToken).ConfigureAwait(false);
        if (text.Length == 0) return;

        // A text element may contain a surrogate pair or a ZWJ sequence. Build
        // one native batch per element so a cancellation never splits a pair.
        var inputs = new NativeMethods.INPUT[text.Length * 2];
        var index = 0;
        foreach (var codeUnit in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            inputs[index++] = NativeMethods.INPUT.Unicode(codeUnit, false);
            inputs[index++] = NativeMethods.INPUT.Unicode(codeUnit, true);
        }
        Send(inputs);
    }

    public async Task SendEnterAsync(bool withShift, CancellationToken cancellationToken)
    {
        await WaitForModifiersReleasedAsync(cancellationToken).ConfigureAwait(false);
        SendKey(Enter, withShift ? Shift : null);
    }

    public async Task SendTabAsync(CancellationToken cancellationToken)
    {
        await WaitForModifiersReleasedAsync(cancellationToken).ConfigureAwait(false);
        SendKey(Tab, null);
    }

    private static void SendKey(ushort key, int? modifier)
    {
        var inputs = modifier is null
            ? new[] { NativeMethods.INPUT.VirtualKey(key, false), NativeMethods.INPUT.VirtualKey(key, true) }
            : new[]
            {
                NativeMethods.INPUT.VirtualKey((ushort)modifier.Value, false),
                NativeMethods.INPUT.VirtualKey(key, false),
                NativeMethods.INPUT.VirtualKey(key, true),
                NativeMethods.INPUT.VirtualKey((ushort)modifier.Value, true)
            };
        Send(inputs);
    }

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
            throw new InputSessionException($"SendInput 只提交了 {sent}/{inputs.Length} 个事件。", new Win32Exception(Marshal.GetLastWin32Error()));
    }

    private static async Task WaitForModifiersReleasedAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (IsModifierDown())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                throw new InputSessionException("快捷键修饰键仍处于按下状态，已停止发送以避免误触发快捷键。");
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsModifierDown() =>
        IsDown(Shift) || IsDown(Control) || IsDown(Alt) || IsDown(Windows);

    private static bool IsDown(int key) => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0;
}
