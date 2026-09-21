# 贡献指南

## 开发环境

- Windows 11 x64 优先；使用当前受支持的 .NET 8 SDK。
- 不把 `.dotnet/`、构建产物、真实网页、账号、答案或输入正文提交到仓库。
- 默认不需要管理员权限；不要通过提权绕过 UIPI。

## 验证

```powershell
dotnet build TextEntryAssistant.sln --configuration Release
dotnet run --project tests/TextEntryAssistant.Core.Tests --configuration Release
```

修改输入路径时同时覆盖中文、英文、标点、emoji、CRLF/LF/CR、暂停、继续、停止、目标切换和部分失败。桌面结果写入 `docs/compatibility.md`，注明系统、应用版本、后端、内容类型和限制。

## 隐私

测试必须使用合成内容。日志和 issue 不应包含正文、剪贴板文本、完整窗口标题、真实课程页面源码或个人路径。
