# 兼容性记录

状态含义：**已验证**表示有可复现的本地人工或原型证据；**自动测试**只代表替换后端/状态机验证；**未验证**不推断兼容或不兼容。

| 系统 | 应用 / 版本 | 后端 | 内容 | 结果 | 限制和证据 |
|---|---|---|---|---|---|
| Windows 11 x64，OS build 26200 | Notepad，Windows 11 内置版本 | `SendInput` Unicode | 中文、英文、标点、emoji、换行 | 已验证（原型） | 2026-09-21；捕获类名 `Notepad`，UI Automation 读取到中文和 emoji；紧急停止在约 400ms 内停止后续调度。 |
| Windows 11 x64 | Edge | `SendInput` Unicode | 未执行 | 未验证 | 需要人工确认普通输入框、iframe 富文本、换行是否触发提交。 |
| Windows 11 x64 | Chrome | `SendInput` Unicode | 未执行 | 未验证 | 需要人工确认普通输入框、iframe 富文本、同窗口焦点变化。 |
| Windows 11 x64 | Word | `SendInput` Unicode | 未执行 | 未验证 | 不声明 Office 兼容性。 |
| Windows 11 x64 | 真实课程平台 | `SendInput` Unicode | 未执行 | 未验证 | 不使用真实账号做自动测试；保存和刷新需用户在测试内容上明确操作。 |

## 原型环境

- SDK：工作区安装 `.NET 8.0.425`；运行时为 Windows Desktop 8.0.31。
- 输入结构按 x64 `INPUT` 布局发送 Unicode UTF-16 code units；emoji 代理对在同一文本元素批次内发送。
- 事件成功只说明进入系统输入流，不说明目标应用接收或服务器保存。
