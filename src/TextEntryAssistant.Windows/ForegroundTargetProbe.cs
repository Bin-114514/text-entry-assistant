using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using TextEntryAssistant.Core;

namespace TextEntryAssistant.Windows;

public sealed class ForegroundTargetProbe : ITargetProbe
{
    public InputTargetSnapshot? Capture()
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle)) return null;
        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        var className = new StringBuilder(256);
        NativeMethods.GetClassName(handle, className, className.Capacity);
        return new InputTargetSnapshot(handle, processId, className.ToString(), TryGetFocusedRuntimeId());
    }

    public bool IsValid(InputTargetSnapshot target)
    {
        if (!NativeMethods.IsWindow(target.WindowHandle) || NativeMethods.GetForegroundWindow() != target.WindowHandle)
            return false;
        if (target.FocusedRuntimeId is null) return true;
        var current = TryGetFocusedRuntimeId();
        return current is null || string.Equals(current, target.FocusedRuntimeId, StringComparison.Ordinal);
    }

    private static string? TryGetFocusedRuntimeId()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            var runtimeId = element?.GetRuntimeId();
            return runtimeId is null ? null : string.Join('.', runtimeId);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }
}
