# 文字输入助手

面向 Windows 11 x64 的本地桌面文字输入工具。把文字准备在窗口里，点击目标应用的输入位置，再使用全局快捷键按小批次发送到当前光标。

## 当前版本

`v0.1.0-preview.2`。这是预览版：核心状态机、Unicode `SendInput` 后端、目标窗口检查、全局快捷键和 WPF 界面已经实现，兼容性仍以[记录](docs/compatibility.md)为准。

## 构建和启动

仓库包含 Windows x64 的 `.NET 8.0.425` SDK 选型记录；开发机需要安装受支持的 .NET 8 SDK。构建：

```powershell
dotnet build TextEntryAssistant.sln --configuration Release
dotnet run --project tests/TextEntryAssistant.Core.Tests --configuration Release
```

启动：

```powershell
dotnet run --project src/TextEntryAssistant.App --configuration Release
```

生成便携包（Windows x64、自包含）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

脚本将 ZIP 和 SHA-256 写入 `artifacts/`。未配置代码签名，Windows 可能显示未签名应用提示。

## 使用

1. 在准备窗口中手动输入或粘贴正文；工具默认不读取剪贴板，也不保存正文。
2. 点击目标输入区域，在目标应用前台按默认的 `Ctrl+Alt+I`。工具会捕获前台 HWND、进程 ID 和窗口类名。
3. 用“输入速度”滑条快速调节 0–500 毫秒的间隔；间隔越小越快。需要更慢的节奏时，也可以在旁边直接输入 0–5000 毫秒。
4. `Ctrl+Alt+P` 暂停或继续，继续前会重新检查目标；`Ctrl+Alt+Esc` 紧急停止。
5. 快捷键可在窗口中修改。若操作系统报告组合键已被占用，换用其他组合后重新应用。

默认遇到换行会暂停并询问使用 Enter、Shift+Enter 或取消；Tab 也会暂停询问。目标应用可能把 Enter 当作发送消息，选择后仍需由用户确认目标行为。选中的目标内容遵循目标控件自己的选区规则，可能被替换。

## 隐私和限制

- 正文、剪贴板内容和完整窗口标题不写入日志或设置；不上传、不自动提交表单或发送聊天消息。
- `SendInput` 只表示事件进入系统输入流，不证明目标控件接收、网页编辑器状态同步或服务器保存成功。
- 不提权、不修改 `isTrusted`，不静默回退到剪贴板；UIPI、应用实现和权限级别可能阻止输入。
- 目标窗口切换或关闭会停止后续批次。同一窗口内部焦点变化尽力通过 UI Automation RuntimeId 检查，信息缺失时覆盖有限。
- 停止不能撤回已经提交的系统事件；工具不提供通用撤销。

## 测试和兼容性

核心测试使用可替换注入器和时钟，覆盖文本元素顺序、代理对、换行规划、重复启动、取消、目标失效、部分状态转换和错误传播。合成网页夹具位于 `fixtures/web/`，不包含真实课程页面、账号或答案。

当前仅对 Windows 11 x64 + Notepad 做过本地 SendInput 原型验证；Edge、Chrome、Word 和真实课程平台在兼容表中标记为“未验证”，需要有相应环境的用户追加人工记录。CI 通过不等于桌面输入端到端验证通过。

## 贡献和安全

请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。安全问题请按 [SECURITY.md](SECURITY.md) 私下报告，不要在公开 issue 粘贴正文、账号、真实网页源码或窗口标题。

项目采用 MIT 许可证，见 [LICENSE](LICENSE)。
