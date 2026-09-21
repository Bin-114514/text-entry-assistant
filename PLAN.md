# 文字输入助手实施计划

## 选择与原则

- 目标框架：`.NET 8` LTS + WPF；本机原先只有运行时，已在工作区安装 SDK `8.0.425` 供可复现构建使用。
- 最小垂直切片先验证 Windows `SendInput`，再接状态机和 UI；任何后端替换都只改 `ITextInjector` 实现。
- 目标快照只保留 HWND、进程 ID、类名和存在性，不记录完整标题或正文。
- 核心测试使用假的时钟、目标和注入器，不依赖真实等待；Windows 原生测试单独标记环境要求。

## 数据流

```text
准备窗口正文
      │ Start
      ▼
文本计划器 ──► Unicode 文本元素/暂停点 ──► 会话状态机
                                               │ 校验 HWND + 焦点
                                               ▼
                                      ITextInjector (SendInput)
                                               │
                                      当前前台焦点控件
```

## 状态与停止语义

```text
Idle ──捕获目标──► Armed ──Start──► Running ──完成──► Completed
  ▲                 │                  │  ▲
  │                 │ Pause            │  │ Resume + 重新校验
  │                 ▼                  ▼  │
  └────取消/失败── Cancelled ◄──── Paused
                         ▲
                         └──目标切换/关闭、紧急停止、注入失败
```

每个会话创建自己的取消令牌；状态机拒绝重复启动。停止只保证不再调度后续批次，已提交系统的事件不可撤回。

## 阶段

1. **环境与原型**：固定 SDK、初始化 Git，编译一个最小 `SendInput` 原型，向记事本输入合成文本并验证紧急停止；在 `docs/compatibility.md` 记录结果和限制。
2. **核心**：实现文本元素分段、换行/Tab 暂停策略、目标快照、会话状态机和可替换接口；先写自动测试。
3. **Windows 适配**：实现 Unicode `SendInput`、全局热键、前台窗口/焦点监测和诊断；注册冲突不覆盖已有热键。
4. **WPF 界面**：文本编辑区、目标捕获、快捷键配置、间隔、换行模式、进度和错误；长文本异步执行，UI 线程只接收进度。
5. **夹具与验证**：提供合成 textarea、contenteditable、同源 iframe、受控组件和拦截 paste 的页面；使用浏览器工具做页面状态检查，桌面输入另做人工记录。
6. **发布**：补齐 README、LICENSE、CHANGELOG、CONTRIBUTING、SECURITY、CI 和发布脚本，执行 review，打包 win-x64 便携 ZIP 与 SHA-256。

## 风险与缓解

| 风险 | 缓解 |
|---|---|
| SendInput 被 UIPI 或应用拦截 | 不提权；显示目标进程级别/错误诊断；记录“事件已提交”而非“保存成功”。 |
| Enter 在聊天工具中发送消息 | 默认遇到换行暂停，要求用户选择 Enter、Shift+Enter 或取消并在界面显示模式。 |
| emoji 被拆成两个 UTF-16 单元 | 使用 `.NET` 文本元素枚举，注入时按代理对组成单元发送。 |
| 同一窗口内焦点变化 | 用 UI Automation 尽力获取焦点控件 RuntimeId；无法确认就停止，不切换到其他窗口重试。 |
| 长文本停止不及时 | 小批次 + 可取消延迟；测试最大停止延迟并公布限制。 |

## 暂不做

TSF 输入法、浏览器扩展、剪贴板后端、云同步、AI 改写、自动表单提交、多平台和真实课程账号自动化。

## NOT in scope

- **TSF 输入法服务**：首版先验证桌面 `SendInput`，避免把系统输入法注册和安装复杂度带入预览版。
- **剪贴板回退**：会与用户剪贴板竞争，且不能保证粘贴事件行为；未来若增加必须由用户显式选择。
- **真实平台自动提交**：提交和服务器保存无法由输入事件证明，交给用户在合成或测试内容上操作。
- **Edge/Chrome/Word 端到端承诺**：当前桌面环境只验证了 Notepad，其他应用列为未验证。

## 实施结果

- 已完成工作区 `.NET 8.0.425` SDK 固定、Git 初始化和输入原型；原型修复了 x64 `INPUT` union 需要 32 字节的 Win32 87 错误。
- 已完成 Core/Windows/App 三层实现、全局热键、前台目标/UIA 尽力校验、暂停点、取消和 WPF 界面。
- 已完成 7 个核心自动测试、合成网页夹具、Windows CI、便携包脚本和 `v0.1.0-preview.1` 自包含 ZIP。

## 评审覆盖图

```text
代码路径                                        用户路径
[+] TextPlan.Create(text)                       [+] 准备正文 → 目标前台 → 全局开始
  ├── [★★★ 已测] CRLF/LF/CR 归一化              ├── [★★★ 原型] Notepad 中文/英文/emoji
  ├── [★★★ 已测] emoji/ZWJ 文本元素             ├── [★★★ 已测] 重复开始被拒绝
  └── [★★★ 已测] Tab/Newline token              ├── [★★★ 已测] 目标失效停止
                                                  ├── [★★★ 已测] 换行暂停 → Shift+Enter
[+] InputSession.RunAsync                       ├── [★★★ 已测] 部分注入失败显示错误
  ├── [★★★ 已测] 顺序与进度                    └── [→E2E 未测] Edge/Chrome/iframe 桌面输入
  ├── [★★★ 已测] 取消不调度后续批次
  ├── [★★★ 已测] 目标校验失败
  ├── [★★★ 已测] 注入异常 → Failed
  └── [★★★ 已测] 手动暂停/恢复信号
[+] SendInputTextInjector                       [+] WPF UI
  ├── [★★★ 原型] x64 struct + Unicode          ├── [★ 启动烟测] 窗口创建和默认热键注册
  ├── [★★ 需桌面] modifier release timeout     └── [→E2E 未测] MessageBox 换行决策
  └── [★★ 需桌面] UIPI / 应用拦截诊断
```

当前覆盖：核心代码路径 12/12 自动或原型覆盖；用户路径 6/8（2 条需要真实 Edge/Chrome 桌面环境）。

## 失败模式审查

| 代码路径 | 生产失败 | 测试 | 错误处理 | 用户反馈 |
|---|---|---|---|---|
| SendInput | UIPI/目标应用拒绝事件 | 原型覆盖成功路径；权限差异未覆盖 | 报告提交数量和通用 Win32 错误 | Failed 状态和重试前重新捕获 |
| 目标探测 | 窗口切换或 HWND 关闭 | 已测替换探针 | 每批次停止，暂停恢复重新校验 | 显示目标变化并停止 |
| UI Automation | 焦点 RuntimeId 不可用 | 未测真实控件 | 信息缺失时尽力放行并记录覆盖限制 | README 明示局限 |
| 热键 | 注册冲突 | 启动烟测；冲突需人工 | `RegisterHotKey` 错误映射 | UI 提示换组合键 |
| 长文本取消 | 已提交批次不可撤回 | 已测取消批次 | 独立令牌，小批次 | 明确“后续不再调度” |
| 换行/Tab | Enter 触发聊天发送或 Tab 切控件 | 换行决策已测，真实应用未测 | 默认暂停询问 | MessageBox 和界面模式提示 |

未发现同时缺少测试、错误处理和用户反馈的静默关键路径；桌面 E2E 缺口已在兼容表标记为未验证。

## 并行化

依赖关系如下：

| 步骤 | 模块 | 依赖 |
|---|---|---|
| 核心状态机与测试 | `src/TextEntryAssistant.Core`, `tests/` | — |
| Windows 输入适配 | `src/TextEntryAssistant.Windows` | 核心接口 |
| WPF 界面 | `src/TextEntryAssistant.App` | 核心、Windows |
| 夹具与文档/CI/打包 | `fixtures/`, `docs/`, `.github/`, `scripts/` | 接口和验收结果 |

实现已按依赖顺序串行完成；后续维护可让文档/夹具与核心测试并行，但本次没有拆分工作树以避免共享接口冲突。

## Implementation Tasks

本轮评审发现的高优先级问题均已在实现中解决；没有留下新的阻塞任务。

- _No new tasks from Architecture._
- _No new tasks from Code Quality._
- _No new tasks from Tests._
- _No new tasks from Performance._

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | 未运行；首版范围由 AGENTS.md 固定 |
| Codex Review | `/codex review` | Independent 2nd opinion | 0 | — | 未运行；当前环境没有 Codex CLI outside voice |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 2 | CLEAR (DIFF) | 0 unresolved, 0 critical gaps；计划审查和提交前差异审查均通过 |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | — | 简单 WPF UI，未单独运行设计审查 |
| DX Review | `/plan-devex-review` | Developer experience gaps | 0 | — | 未运行 |

**VERDICT:** ENG CLEARED — ready to ship after final diff review;桌面 Edge/Chrome/Word 仍需用户环境追加验证。

NO UNRESOLVED DECISIONS
