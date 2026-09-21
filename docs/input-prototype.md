# SendInput 原型记录

原型位于 `prototypes/SendInputPrototype/`，会启动一个由原型自己创建的 Notepad 窗口，捕获前台 HWND 后发送合成文本 `文字输入助手 / SendInput prototype 😀` 和换行，再以 25ms 间隔发送可取消的 `x` 批次。

2026-09-21 在 Windows 11 x64（OS build 26200）执行：

- 首次运行暴露 x64 `INPUT` union 尺寸错误（`SendInput` 返回 Win32 87）；将 union 固定为 32 字节后修复。
- 捕获目标输出 `class=Notepad`，UI Automation Edit 控件读取验证中文和 emoji 均存在。
- 400ms 紧急停止后只完成 13–14 个额外字符，任务没有调度后续批次。
- 该验证只覆盖 Notepad 和合成内容；不能推出 Edge、Chrome、Word、iframe 编辑器或服务器保存成功。
