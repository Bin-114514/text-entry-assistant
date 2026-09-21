namespace TextEntryAssistant.Core;

public sealed class InputSession : IDisposable
{
    private readonly ITextInjector _injector;
    private readonly ITargetProbe _targetProbe;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly object _gate = new();
    private IReadOnlyList<InputUnit> _units = [];
    private InputSettings _settings = InputSettings.Default;
    private InputTargetSnapshot? _target;
    private CancellationTokenSource? _runCancellation;
    private TaskCompletionSource<bool> _resumeSignal = CompletedSignal();
    private TaskCompletionSource<BreakDecision>? _breakSignal;
    private InputSessionState _state = InputSessionState.Idle;
    private InputProgress _progress = new(0, 0);
    private string? _errorMessage;
    private bool _disposed;

    public InputSession(
        ITextInjector injector,
        ITargetProbe targetProbe,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
        _targetProbe = targetProbe ?? throw new ArgumentNullException(nameof(targetProbe));
        _delay = delay ?? Task.Delay;
    }

    public InputSessionState State { get { lock (_gate) return _state; } }
    public InputProgress Progress { get { lock (_gate) return _progress; } }
    public InputTargetSnapshot? Target { get { lock (_gate) return _target; } }
    public string? ErrorMessage { get { lock (_gate) return _errorMessage; } }
    public event Action<InputSessionState>? StateChanged;
    public event Action<InputProgress>? ProgressChanged;
    public event EventHandler<BreakRequestedEventArgs>? BreakRequested;

    public bool Arm(string text, InputTargetSnapshot target, InputSettings settings)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("输入正文不能为空。", nameof(text));
        if (settings.Interval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(settings), "输入间隔不能为负数。");

        lock (_gate)
        {
            if (_state is InputSessionState.Running or InputSessionState.Paused)
                return false;
            _units = TextPlan.Create(text);
            _settings = settings;
            _target = target;
            _progress = new InputProgress(0, _units.Count);
            _errorMessage = null;
            _state = InputSessionState.Armed;
        }
        PublishState(InputSessionState.Armed);
        PublishProgress(_progress);
        return true;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_state != InputSessionState.Armed)
                throw new InvalidOperationException("只有 Armed 状态可以开始输入。");
            if (_target is null || !_targetProbe.IsValid(_target))
            {
                _errorMessage = "目标窗口已失效，请重新捕获目标后再试。";
                _state = InputSessionState.Failed;
                PublishState(InputSessionState.Failed);
                return Task.CompletedTask;
            }
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _resumeSignal = CompletedSignal();
            _state = InputSessionState.Running;
        }
        PublishState(InputSessionState.Running);
        return RunAsync(_runCancellation.Token);
    }

    public bool Pause()
    {
        lock (_gate)
        {
            if (_state != InputSessionState.Running || _breakSignal is not null)
                return false;
            _resumeSignal = NewSignal();
            _state = InputSessionState.Paused;
        }
        PublishState(InputSessionState.Paused);
        return true;
    }

    public bool Resume()
    {
        lock (_gate)
        {
            if (_state != InputSessionState.Paused || _breakSignal is not null || _target is null)
                return false;
            if (!_targetProbe.IsValid(_target))
            {
                CancelUnsafe("目标窗口或焦点已变化，请重新捕获目标。");
                PublishState(InputSessionState.Cancelled);
                return false;
            }
            _state = InputSessionState.Running;
            _resumeSignal.TrySetResult(true);
        }
        PublishState(InputSessionState.Running);
        return true;
    }

    public bool ResolveBreak(BreakDecision decision)
    {
        TaskCompletionSource<BreakDecision>? signal;
        lock (_gate)
        {
            signal = _breakSignal;
            if (signal is null || _state != InputSessionState.Paused)
                return false;
            _breakSignal = null;
            if (decision == BreakDecision.Cancel)
                _state = InputSessionState.Cancelled;
            else
                _state = InputSessionState.Running;
        }
        signal.TrySetResult(decision);
        PublishState(State);
        return true;
    }

    public void Cancel(string reason = "用户已停止输入。")
    {
        lock (_gate)
        {
            if (_state is InputSessionState.Idle or InputSessionState.Completed or InputSessionState.Cancelled or InputSessionState.Failed)
                return;
            CancelUnsafe(reason);
        }
        PublishState(InputSessionState.Cancelled);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (var index = 0; index < _units.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Target;
                if (target is null || !_targetProbe.IsValid(target))
                    throw new InputSessionException("目标窗口或焦点已变化，输入已停止。");

                var unit = _units[index];
                switch (unit.Kind)
                {
                    case InputUnitKind.Text:
                        await _injector.SendTextAsync(unit.Value, cancellationToken).ConfigureAwait(false);
                        break;
                    case InputUnitKind.Newline:
                        await SendNewlineAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case InputUnitKind.Tab:
                        await SendTabAsync(cancellationToken).ConfigureAwait(false);
                        break;
                }

                lock (_gate) _progress = new InputProgress(index + 1, _units.Count);
                PublishProgress(Progress);
                if (index + 1 < _units.Count)
                    await _delay(_settings.Interval, cancellationToken).ConfigureAwait(false);
                await WaitForResumeAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_gate) _state = InputSessionState.Completed;
            PublishState(InputSessionState.Completed);
        }
        catch (OperationCanceledException) when (State == InputSessionState.Cancelled)
        {
            PublishState(InputSessionState.Cancelled);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _errorMessage = ex is InputSessionException ? ex.Message : "输入失败，请检查目标应用和权限。";
                _state = InputSessionState.Failed;
            }
            PublishState(InputSessionState.Failed);
        }
        finally
        {
            lock (_gate)
            {
                _runCancellation?.Dispose();
                _runCancellation = null;
                _breakSignal = null;
                _resumeSignal.TrySetResult(false);
            }
        }
    }

    private async Task SendNewlineAsync(CancellationToken cancellationToken)
    {
        if (_settings.NewlineMode == NewlineMode.Enter)
        {
            await _injector.SendEnterAsync(false, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (_settings.NewlineMode == NewlineMode.ShiftEnter)
        {
            await _injector.SendEnterAsync(true, cancellationToken).ConfigureAwait(false);
            return;
        }
        var decision = await RequestBreakAsync(InputUnitKind.Newline, "检测到换行：选择 Enter、Shift+Enter 或取消本次输入。", cancellationToken).ConfigureAwait(false);
        if (decision == BreakDecision.Cancel)
            throw new OperationCanceledException(cancellationToken);
        await _injector.SendEnterAsync(decision == BreakDecision.ShiftEnter, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendTabAsync(CancellationToken cancellationToken)
    {
        if (_settings.PauseOnTab)
        {
            var decision = await RequestBreakAsync(InputUnitKind.Tab, "检测到 Tab：选择发送 Tab 或取消本次输入。", cancellationToken).ConfigureAwait(false);
            if (decision == BreakDecision.Cancel)
                throw new OperationCanceledException(cancellationToken);
        }
        await _injector.SendTabAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<BreakDecision> RequestBreakAsync(InputUnitKind kind, string message, CancellationToken cancellationToken)
    {
        TaskCompletionSource<BreakDecision> signal;
        lock (_gate)
        {
            _state = InputSessionState.Paused;
            signal = new TaskCompletionSource<BreakDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            _breakSignal = signal;
        }
        PublishState(InputSessionState.Paused);
        BreakRequested?.Invoke(this, new BreakRequestedEventArgs(kind, message));
        return await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForResumeAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> signal;
        lock (_gate) signal = _resumeSignal;
        await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void CancelUnsafe(string reason)
    {
        _errorMessage = reason;
        _state = InputSessionState.Cancelled;
        _runCancellation?.Cancel();
        _resumeSignal.TrySetResult(false);
        _breakSignal?.TrySetResult(BreakDecision.Cancel);
    }

    private void PublishState(InputSessionState state) => StateChanged?.Invoke(state);
    private void PublishProgress(InputProgress progress) => ProgressChanged?.Invoke(progress);
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(InputSession)); }
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource<bool> CompletedSignal() { var signal = NewSignal(); signal.TrySetResult(true); return signal; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel("应用正在关闭。");
    }
}
