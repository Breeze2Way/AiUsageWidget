# AiUsageWidget / AI 用量悬浮球

AiUsageWidget combines local Codex usage data and Antigravity quota data in one Windows floating widget. The tooltip shows both providers; the floating ball follows whichever supported application is in the foreground.

AiUsageWidget 把本机 Codex 用量与 Antigravity 配额合并显示在一个 Windows 悬浮球中。悬停详情同时列出两个来源；悬浮球按当前前台应用切换百分比。

## Features / 功能

- Floating center/fill shows the active provider's five-hour remaining percentage; the outer ring shows that provider's weekly remaining percentage.
  悬浮球中心和填充显示当前激活来源的五小时剩余比例，外环显示同一来源的周剩余比例。
- The tooltip keeps Gemini/Claude groups and a separate Codex row, with independent today/yesterday token totals and reset times.
  悬停详情保留 Gemini/Claude 分组，并单独显示 Codex 行；今日/昨日 token 与重置时间按来源分别显示。
- Foreground ChatGPT/Codex selects Codex. Foreground Antigravity selects Antigravity. Other windows retain the last selection; startup defaults to Antigravity.
  前台为 ChatGPT/Codex 时选 Codex，前台为 Antigravity 时选 Antigravity；其他窗口保持最近一次选择，启动默认选择 Antigravity。
- Chinese and English text follow the language configured in the widget.
  中英文显示完全跟随小工具中的语言设置。
- Tray controls, settings, startup behavior, multi-monitor placement, single-instance protection, and the current Antigravity icon are retained.
  保留托盘菜单、设置、开机启动、多显示器位置、单实例保护和当前 Antigravity 图标。
- An optional Codex weekly token budget can be configured for local usage estimates.
  可选设置 Codex 周 token 预算，用于本地用量估算。

## Requirements / 运行要求

- Windows 10/11 x64 and .NET 9 Desktop Runtime.
  Windows 10/11 x64 和 .NET 9 Desktop Runtime。
- Antigravity must be signed in and running for live model quotas. Daily token totals are read from its local conversation databases.
  要实时读取 Antigravity 模型配额，需要登录并运行 Antigravity；每日 token 数从本地会话数据库读取。
- Codex usage uses the local `%USERPROFILE%\.codex` state database, session files, and signed-in auth data.
  Codex 用量从 `%USERPROFILE%\.codex` 下的状态数据库、会话文件和本机登录信息读取。

## Build and publish / 构建与发布

```powershell
dotnet build src\AiUsageWidget\AiUsageWidget.csproj -c Release
.\publish.ps1
```

The publish script creates `publish\AiUsageWidget.exe` as a win-x64 single-file app that uses the installed .NET Desktop Runtime. Keep `悬浮球.ico` beside the executable so the custom window and tray icon can load.

发布脚本生成 `publish\AiUsageWidget.exe`。程序为 win-x64 单文件，并使用系统已安装的 .NET Desktop Runtime。请将 `悬浮球.ico` 与 exe 放在一起，供窗口和托盘加载自定义图标。

## Data and privacy / 数据与隐私

- Antigravity quota is read in read-only mode from its local language server. Token totals are read from local conversation databases.
  Antigravity 配额以只读方式从本机 language server 获取；token 总量从本机对话数据库读取。
- Codex token totals are calculated from local Codex session records. Five-hour and weekly quota data use the signed-in Codex account's usage endpoint.
  Codex token 总量根据本机 session 记录统计；五小时和周额度通过已登录 Codex 账户的用量接口读取。
- Credentials are used only for the local request and are not displayed, logged, or uploaded by this app.
  凭据只用于本机发起请求，不由本程序显示、写入日志或上传。
- Settings and error logs are stored separately under `%LOCALAPPDATA%\AiUsageWidget`.
  设置和错误日志独立保存在 `%LOCALAPPDATA%\AiUsageWidget`。

## Project layout / 项目结构

- `src\AiUsageWidget`: combined desktop app / 合并后的桌面程序
- `src\AiUsageWidget\Codex`: Codex reader and usage calculations / Codex 读取与统计模块
- `tests\AiUsageWidget.Tests`: carried-over application checks / 继承的应用检查
