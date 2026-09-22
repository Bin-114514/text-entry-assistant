namespace TextEntryAssistant.Core;

public enum InputSessionState
{
    Idle,
    Armed,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed
}

public enum InputUnitKind
{
    Text,
    Newline,
    Tab
}

public enum NewlineMode
{
    Pause,
    Enter,
    ShiftEnter
}

public enum InputSpeedMode
{
    Compatible,
    Balanced,
    Fastest
}

public enum BreakDecision
{
    Enter,
    ShiftEnter,
    Tab,
    Cancel
}

public sealed record InputUnit(InputUnitKind Kind, string Value);

public sealed record InputTargetSnapshot(
    nint WindowHandle,
    uint ProcessId,
    string WindowClassName,
    string? FocusedRuntimeId = null);

public sealed record InputSettings(
    TimeSpan Interval,
    NewlineMode NewlineMode = NewlineMode.Pause,
    bool PauseOnTab = true,
    InputSpeedMode SpeedMode = InputSpeedMode.Balanced)
{
    public static InputSettings Default { get; } = new(TimeSpan.FromMilliseconds(35));
}

public sealed record InputProgress(int Completed, int Total)
{
    public double Fraction => Total == 0 ? 0 : (double)Completed / Total;
}

public sealed class BreakRequestedEventArgs(InputUnitKind kind, string message) : EventArgs
{
    public InputUnitKind Kind { get; } = kind;
    public string Message { get; } = message;
}

public sealed class InputSessionException(string message, Exception? inner = null) : Exception(message, inner);

public interface ITextInjector
{
    Task SendTextAsync(string text, CancellationToken cancellationToken);
    Task SendEnterAsync(bool withShift, CancellationToken cancellationToken);
    Task SendTabAsync(CancellationToken cancellationToken);
}

public interface ITargetProbe
{
    InputTargetSnapshot? Capture();
    bool IsValid(InputTargetSnapshot target);
}
