using TextEntryAssistant.Core;

namespace TextEntryAssistant.Core.Tests;

public sealed class InputSessionTests
{
    public static async Task SendsInOrderAndCompletes()
    {
        var injector = new RecordingInjector();
        var probe = new RecordingProbe();
        using var session = new InputSession(injector, probe, static (_, _) => Task.CompletedTask);
        TestAssert.True(session.Arm("A😀B", probe.Target, new InputSettings(TimeSpan.Zero)));

        await session.StartAsync();

        TestAssert.Equal(InputSessionState.Completed, session.State);
        TestAssert.SequenceEqual(["A", "😀", "B"], injector.Calls);
        TestAssert.Equal(3, session.Progress.Completed);
    }

    public static async Task RejectsDuplicateStartAndCancelsFutureBatches()
    {
        var injector = new RecordingInjector { GateFirstCall = true };
        var probe = new RecordingProbe();
        using var session = new InputSession(injector, probe, static (_, _) => Task.CompletedTask);
        session.Arm("ABCDE", probe.Target, new InputSettings(TimeSpan.Zero));
        var running = session.StartAsync();

        await injector.FirstCallStarted.Task;
        TestAssert.Throws<InvalidOperationException>(() => session.StartAsync());
        session.Cancel();
        injector.ReleaseFirstCall.TrySetResult(true);
        await running;

        TestAssert.Equal(InputSessionState.Cancelled, session.State);
        TestAssert.Equal(1, injector.Calls.Count);
    }

    public static async Task StopsWhenTargetChangesBeforeNextUnit()
    {
        var injector = new RecordingInjector();
        var probe = new RecordingProbe { Valid = true };
        using var session = new InputSession(injector, probe, static (_, _) => Task.CompletedTask);
        session.Arm("AB", probe.Target, new InputSettings(TimeSpan.Zero));
        probe.Valid = false;

        await session.StartAsync();

        TestAssert.Equal(InputSessionState.Failed, session.State);
        TestAssert.Empty(injector.Calls);
        TestAssert.Contains("目标窗口", session.ErrorMessage);
    }

    public static async Task PausesOnNewlineUntilUserChoosesShiftEnter()
    {
        var injector = new RecordingInjector();
        var probe = new RecordingProbe();
        using var session = new InputSession(injector, probe, static (_, _) => Task.CompletedTask);
        session.Arm("A\nB", probe.Target, new InputSettings(TimeSpan.Zero, NewlineMode.Pause));
        var requested = new TaskCompletionSource<BreakRequestedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.BreakRequested += (_, args) => requested.TrySetResult(args);
        var running = session.StartAsync();

        var request = await requested.Task.WaitAsync(TimeSpan.FromSeconds(1));
        TestAssert.Equal(InputUnitKind.Newline, request.Kind);
        TestAssert.Equal(InputSessionState.Paused, session.State);
        TestAssert.True(session.ResolveBreak(BreakDecision.ShiftEnter));
        await running;

        TestAssert.SequenceEqual(["A", "shift-enter", "B"], injector.Calls);
        TestAssert.Equal(InputSessionState.Completed, session.State);
    }

    public static async Task PropagatesPartialSendFailure()
    {
        var injector = new RecordingInjector { ThrowOnCall = 2 };
        var probe = new RecordingProbe();
        using var session = new InputSession(injector, probe, static (_, _) => Task.CompletedTask);
        session.Arm("ABC", probe.Target, new InputSettings(TimeSpan.Zero));

        await session.StartAsync();

        TestAssert.Equal(InputSessionState.Failed, session.State);
        TestAssert.Equal(1, session.Progress.Completed);
        TestAssert.Contains("失败", session.ErrorMessage);
    }

    private sealed class RecordingProbe : ITargetProbe
    {
        public InputTargetSnapshot Target { get; } = new(1, 2, "Test");
        public bool Valid { get; set; } = true;
        public InputTargetSnapshot? Capture() => Target;
        public bool IsValid(InputTargetSnapshot target) => Valid;
    }

    private sealed class RecordingInjector : ITextInjector
    {
        public List<string> Calls { get; } = [];
        public bool GateFirstCall { get; init; }
        public int ThrowOnCall { get; init; }
        public TaskCompletionSource<bool> FirstCallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseFirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            Calls.Add(text);
            if (ThrowOnCall == Calls.Count) throw new InputSessionException("合成注入器失败");
            if (GateFirstCall && Calls.Count == 1)
            {
                FirstCallStarted.TrySetResult(true);
                await ReleaseFirstCall.Task.WaitAsync(cancellationToken);
            }
        }

        public Task SendEnterAsync(bool withShift, CancellationToken cancellationToken)
        {
            Calls.Add(withShift ? "shift-enter" : "enter");
            return Task.CompletedTask;
        }

        public Task SendTabAsync(CancellationToken cancellationToken)
        {
            Calls.Add("tab");
            return Task.CompletedTask;
        }
    }
}
