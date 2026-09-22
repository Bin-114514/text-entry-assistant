using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Globalization;
using TextEntryAssistant.Core;
using TextEntryAssistant.Windows;

namespace TextEntryAssistant.App;

public partial class MainWindow : Window
{
    private readonly ForegroundTargetProbe _targetProbe = new();
    private readonly SendInputTextInjector _injector = new();
    private readonly HotkeyService _hotkeys = new();
    private InputSession? _session;
    private HotkeyDefinition? _startHotkey;
    private HotkeyDefinition? _pauseHotkey;
    private HotkeyDefinition? _stopHotkey;
    private bool _closing;
    private bool _updatingIntervalControl;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hotkeys.Attach(this);
        _hotkeys.Pressed += Hotkeys_Pressed;
        ApplyHotkeys();
        CaptureTarget();
    }

    private void Hotkeys_Pressed(HotkeyDefinition definition)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (Equals(definition, _startHotkey))
                _ = StartOrResumeAsync();
            else if (Equals(definition, _pauseHotkey))
                TogglePause();
            else if (Equals(definition, _stopHotkey))
                StopSession();
        }, DispatcherPriority.Input);
    }

    private async Task StartOrResumeAsync()
    {
        if (_session?.State == InputSessionState.Paused)
        {
            if (!_session.Resume()) SetStatus("目标已变化，已停止；请重新捕获目标。", isError: true);
            return;
        }
        if (_session?.State == InputSessionState.Running) return;
        if (string.IsNullOrEmpty(ContentBox.Text))
        {
            SetStatus("请先准备要输入的文字。", isError: true);
            return;
        }

        var target = _targetProbe.Capture();
        if (target is null)
        {
            SetStatus("未找到可用的前台窗口。", isError: true);
            return;
        }
        if (!TryReadSettings(out var settings)) return;
        DisposeSession();
        ShowTarget(target);
        _session = new InputSession(_injector, _targetProbe);
        _session.StateChanged += Session_StateChanged;
        _session.ProgressChanged += Session_ProgressChanged;
        _session.BreakRequested += Session_BreakRequested;
        if (!_session.Arm(ContentBox.Text, target, settings)) return;
        SetStatus("已捕获目标，开始输入。", isError: false);
        try { await _session.StartAsync(); }
        catch (Exception ex) when (ex is InputSessionException or InvalidOperationException)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void Session_BreakRequested(object? sender, BreakRequestedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_session is null) return;
            var result = e.Kind == InputUnitKind.Newline
                ? MessageBox.Show(this, e.Message, "需要确认输入方式", MessageBoxButton.YesNoCancel, MessageBoxImage.Information)
                : MessageBox.Show(this, e.Message, "需要确认输入方式", MessageBoxButton.YesNo, MessageBoxImage.Information);
            var decision = e.Kind == InputUnitKind.Newline
                ? result switch
                {
                    MessageBoxResult.Yes => BreakDecision.Enter,
                    MessageBoxResult.No => BreakDecision.ShiftEnter,
                    _ => BreakDecision.Cancel
                }
                : result == MessageBoxResult.Yes ? BreakDecision.Tab : BreakDecision.Cancel;
            _session.ResolveBreak(decision);
        }, DispatcherPriority.Normal);
    }

    private void Session_StateChanged(InputSessionState state)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;
            var error = _session?.ErrorMessage;
            SetStatus(state switch
            {
                InputSessionState.Armed => "已准备，等待开始。",
                InputSessionState.Running => "输入中……",
                InputSessionState.Paused => "已暂停；恢复前会重新检查目标。",
                InputSessionState.Completed => "输入完成；目标应用是否保存由应用自身决定。",
                InputSessionState.Cancelled => error ?? "已停止，不再调度后续批次。",
                InputSessionState.Failed => error ?? "输入失败。",
                _ => "就绪"
            }, state is InputSessionState.Failed or InputSessionState.Cancelled);
            var active = state is InputSessionState.Running or InputSessionState.Paused;
            PauseButton.IsEnabled = active;
            StopButton.IsEnabled = active;
            StartButton.IsEnabled = !active;
        });
    }

    private void Session_ProgressChanged(InputProgress progress)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ProgressBar.Value = progress.Fraction;
            StatusLabel.Text = $"已发送 {progress.Completed}/{progress.Total} 个文本元素";
        });
    }

    private async void CaptureTarget_Click(object sender, RoutedEventArgs e)
    {
        // The button itself would become the foreground target. Minimize briefly
        // so the previously active application can be captured instead.
        WindowState = WindowState.Minimized;
        await Task.Delay(250);
        var target = _targetProbe.Capture();
        WindowState = WindowState.Normal;
        Activate();
        if (target is null)
        {
            SetStatus("未找到前台窗口。请改用全局开始快捷键。", isError: true);
            return;
        }
        ShowTarget(target);
        SetStatus("已捕获目标；点击目标输入区域后按开始快捷键可重新校验。", isError: false);
    }

    private void ContentBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ContentPlaceholder.Visibility = string.IsNullOrEmpty(ContentBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void IntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingIntervalControl || IntervalBox is null) return;
        _updatingIntervalControl = true;
        IntervalBox.Text = Math.Round(e.NewValue).ToString(CultureInfo.InvariantCulture);
        _updatingIntervalControl = false;
    }

    private void IntervalBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalBox.Text, out var milliseconds) || milliseconds is < 0 or > 5000) return;
        _updatingIntervalControl = true;
        IntervalSlider.Value = Math.Min(milliseconds, (int)IntervalSlider.Maximum);
        _updatingIntervalControl = false;
    }

    private void CaptureTarget()
    {
        var target = _targetProbe.Capture();
        if (target is null)
        {
            SetStatus("未找到前台窗口。请先点击目标输入区域，再按全局开始快捷键。", isError: true);
            return;
        }
        ShowTarget(target);
        SetStatus("已捕获目标；点击目标输入区域后按开始快捷键可重新校验。", isError: false);
    }

    private void StartButton_Click(object sender, RoutedEventArgs e) => _ = StartOrResumeAsync();
    private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePause();
    private void StopButton_Click(object sender, RoutedEventArgs e) => StopSession();

    private void TogglePause()
    {
        if (_session?.State == InputSessionState.Running) _session.Pause();
        else if (_session?.State == InputSessionState.Paused && !_session.Resume()) SetStatus("目标已变化，已停止。", isError: true);
    }

    private void StopSession()
    {
        _session?.Cancel();
        SetStatus("正在停止；已提交系统的事件无法撤回。", isError: false);
    }

    private void ApplyHotkeys_Click(object sender, RoutedEventArgs e) => ApplyHotkeys();

    private void ApplyHotkeys()
    {
        if (!TryParseHotkey(StartHotkeyBox.Text, "开始") || !TryParseHotkey(PauseHotkeyBox.Text, "暂停") || !TryParseHotkey(StopHotkeyBox.Text, "停止")) return;
        var definitions = new Dictionary<string, HotkeyDefinition>
        {
            ["start"] = HotkeyDefinition.TryParse(StartHotkeyBox.Text, out var start) ? start! : throw new InvalidOperationException(),
            ["pause"] = HotkeyDefinition.TryParse(PauseHotkeyBox.Text, out var pause) ? pause! : throw new InvalidOperationException(),
            ["stop"] = HotkeyDefinition.TryParse(StopHotkeyBox.Text, out var stop) ? stop! : throw new InvalidOperationException()
        };
        try
        {
            _hotkeys.Replace(definitions);
            _startHotkey = definitions["start"];
            _pauseHotkey = definitions["pause"];
            _stopHotkey = definitions["stop"];
            HotkeyStatus.Text = "快捷键已注册。";
            SetStatus("快捷键已更新。", isError: false);
        }
        catch (HotkeyRegistrationException ex)
        {
            HotkeyStatus.Text = ex.Message;
            SetStatus("快捷键注册失败；请换用未占用的组合键。", isError: true);
        }
    }

    private bool TryParseHotkey(string text, string label)
    {
        if (HotkeyDefinition.TryParse(text, out _)) return true;
        SetStatus($"{label}快捷键格式无效，请使用 Ctrl+Alt+I 这类格式。", isError: true);
        return false;
    }

    private bool TryReadSettings(out InputSettings settings)
    {
        settings = InputSettings.Default;
        if (!int.TryParse(IntervalBox.Text, out var milliseconds) || milliseconds is < 0 or > 5000)
        {
            SetStatus("输入间隔应为 0 到 5000 毫秒。", isError: true);
            return false;
        }
        var mode = NewlineModeBox.SelectedItem is ComboBoxItem item && item.Tag is string tag
            ? Enum.Parse<NewlineMode>(tag)
            : NewlineMode.Pause;
        settings = new InputSettings(TimeSpan.FromMilliseconds(milliseconds), mode, PauseOnTabBox.IsChecked == true);
        return true;
    }

    private void ShowTarget(InputTargetSnapshot target) => TargetLabel.Text = $"HWND 0x{target.WindowHandle.ToInt64():X} · 进程 {target.ProcessId} · 类名 {target.WindowClassName}";

    private void SetStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.Foreground = new System.Windows.Media.SolidColorBrush(isError ? System.Windows.Media.Colors.Firebrick : System.Windows.Media.Colors.DimGray);
    }

    private void DisposeSession()
    {
        _session?.Dispose();
        _session = null;
        ProgressBar.Value = 0;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        _session?.Cancel("应用正在关闭。");
        _session?.Dispose();
        _hotkeys.Dispose();
    }
}
