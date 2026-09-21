using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

const string sample = "文字输入助手 / SendInput prototype 😀\r\n";

using var notepad = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true })
    ?? throw new InvalidOperationException("无法启动记事本");

if (!notepad.WaitForInputIdle(5000))
    throw new InvalidOperationException("记事本未进入可交互状态");

await Task.Delay(500);
var target = IntPtr.Zero;
for (var attempt = 0; attempt < 20 && target == IntPtr.Zero; attempt++)
{
    target = FindWindow("Notepad", null);
    if (target == IntPtr.Zero) await Task.Delay(100);
}
if (target == IntPtr.Zero)
    throw new InvalidOperationException("无法找到原型创建的记事本窗口");
SetForegroundWindow(target);
await Task.Delay(150);
var targetPid = GetWindowThreadProcessId(target, out var pid);
var className = new StringBuilder(256);
GetClassName(target, className, className.Capacity);
Console.WriteLine($"Captured target: pid={pid}, class={className}");

var sent = 0;
var enumerator = StringInfo.GetTextElementEnumerator(sample);
while (enumerator.MoveNext())
{
    var element = enumerator.GetTextElement();
    if (element is "\r\n" or "\r" or "\n")
    {
        SendVirtualKey(0x0D);
    }
    else
    {
        SendUnicode(element);
    }

    sent++;
    await Task.Delay(40);
}

Console.WriteLine($"Prototype sent {sent} Unicode text elements to the captured foreground target (HWND 0x{target.ToInt64():X}).");
try
{
    var root = AutomationElement.FromHandle(target);
    var document = root.FindFirst(
        TreeScope.Descendants,
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document))
        ?? root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
    if (document?.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) == true)
    {
        var value = ((ValuePattern)pattern).Current.Value;
        Console.WriteLine($"UIA document verification: {value.Contains("文字输入助手", StringComparison.Ordinal) && value.Contains("😀", StringComparison.Ordinal)}");
    }
    else
    {
        Console.WriteLine("UIA document verification: unavailable for this Notepad build.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"UIA document verification unavailable: {ex.GetType().Name}");
}
Console.WriteLine("紧急停止验证：继续发送 100 个字符，400ms 后取消。");

using var stop = new CancellationTokenSource(400);
var beforeStop = 0;
try
{
    for (var i = 0; i < 100; i++)
    {
        stop.Token.ThrowIfCancellationRequested();
        SendUnicode("x");
        beforeStop++;
        await Task.Delay(25, stop.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine($"Emergency stop observed after {beforeStop} additional characters; no later batches were scheduled.");
}

Console.WriteLine("请在记事本中目视确认第一行包含中文、英文、emoji，并按 Enter 退出；进程将在 10 秒后关闭记事本。");
await Task.Delay(10000);
try { notepad.Kill(entireProcessTree: true); } catch { }
try { Process.GetProcessById((int)targetPid).Kill(entireProcessTree: true); } catch { }

static void SendUnicode(string text)
{
    var inputs = new INPUT[text.Length * 2];
    var index = 0;
    foreach (var codeUnit in text)
    {
        inputs[index++] = INPUT.Unicode(codeUnit, keyUp: false);
        inputs[index++] = INPUT.Unicode(codeUnit, keyUp: true);
    }

    if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != inputs.Length)
        throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput Unicode 失败");
}

static void SendVirtualKey(ushort key)
{
    var inputs = new[] { INPUT.VirtualKey(key, false), INPUT.VirtualKey(key, true) };
    if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != inputs.Length)
        throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput 按键失败");
}

[DllImport("user32.dll", SetLastError = true)]
static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInput);

[DllImport("user32.dll")]
static extern bool SetForegroundWindow(IntPtr hWnd);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern IntPtr FindWindow(string? className, string? windowName);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

[DllImport("user32.dll")]
static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

[StructLayout(LayoutKind.Sequential)]
struct INPUT
{
    public uint type;
    public InputUnion data;

    public static INPUT Unicode(char value, bool keyUp) => new()
    {
        type = 1,
        data = new InputUnion { keyboard = new KEYBDINPUT { wScan = value, dwFlags = 0x0004u | (keyUp ? 0x0002u : 0u) } }
    };

    public static INPUT VirtualKey(ushort value, bool keyUp) => new()
    {
        type = 1,
        data = new InputUnion { keyboard = new KEYBDINPUT { wVk = value, dwFlags = keyUp ? 0x0002u : 0u } }
    };
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
struct InputUnion
{
    [FieldOffset(0)] public KEYBDINPUT keyboard;
}

[StructLayout(LayoutKind.Sequential)]
struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}
