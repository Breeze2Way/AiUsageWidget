# AiUsageWidget / AI 用量悬浮球

AiUsageWidget is a lightweight Windows floating widget for viewing Antigravity and Codex usage in one place. It combines local token totals with five-hour and weekly quota data, while the floating ball automatically follows the supported app currently in the foreground.

AiUsageWidget 是一个轻量的 Windows 用量悬浮球，在同一处展示 Antigravity 与 Codex 的本地 token 统计、五小时额度和周额度。悬浮球会根据当前位于前台的受支持应用自动切换数据来源。

## Features / 功能

- Floating center/fill shows the active provider's five-hour remaining percentage; the outer ring shows that provider's weekly remaining percentage.
  悬浮球中心和填充显示当前激活来源的五小时剩余比例，外环显示同一来源的周剩余比例。
- A compact, wide tooltip shows today, yesterday, 7-day, and 30-day token totals for both providers, plus Gemini, Claude, and Codex quota rows.
  宽版紧凑悬停详情分别显示两种来源的今日、昨日、7 天和 30 天 token 总量，并列出 Gemini、Claude 与 Codex 额度。
- Reset times and countdowns appear directly below each provider's usage row, followed by the latest data refresh timestamp.
  重置时间和剩余倒计时直接显示在对应来源的用量行下方，末尾显示最近的数据更新时间。
- Only the quota row represented by the floating ball is green. For Antigravity this is the currently selected Gemini or Claude model group; for Codex it is the Codex quota row. Every usage, reset, and timestamp row stays white.
  只有悬浮球当前代表的额度行显示为绿色：Antigravity 对应当前选中的 Gemini 或 Claude 模型组，Codex 对应 Codex 额度行；用量、重置和更新时间行均保持白色。
- Antigravity and Codex can each be enabled in settings. The tooltip shows an enabled provider only while its application is running; if one provider is shown, all its tooltip text is white.
  设置中可分别勾选 Antigravity 和 Codex。只有已勾选且对应程序正在运行时才显示其用量；只显示一个来源时，全部提示文字均为白色。
- Foreground ChatGPT/Codex selects Codex. Foreground Antigravity selects Antigravity. Other windows retain the last selection; startup defaults to Antigravity.
  前台为 ChatGPT/Codex 时选 Codex，前台为 Antigravity 时选 Antigravity；其他窗口保持最近一次选择，启动默认选择 Antigravity。
- Both providers are enabled by default. Codex presence includes either the Codex or ChatGPT desktop process.
  默认同时启用两个来源。检测 Codex 时，Codex 或 ChatGPT 桌面程序进程均视为正在运行。
- Chinese and English text follow the language configured in the widget.
  中英文显示完全跟随小工具中的语言设置。
- Tray controls, settings, startup behavior, multi-monitor placement, single-instance protection, and the current Antigravity icon are retained.
  保留托盘菜单、设置、开机启动、多显示器位置、单实例保护和当前 Antigravity 图标。
- An optional Codex weekly token budget can be configured for local usage estimates.
  可选设置 Codex 周 token 预算，用于本地用量估算。

## Requirements / 运行要求

- Windows 10/11 x64 and .NET 9 Desktop Runtime.
  Windows 10/11 x64 和 .NET 9 Desktop Runtime。
- Antigravity must be signed in and running for live model quotas. Token totals are read from its local conversation databases.
  要实时读取 Antigravity 模型配额，需要登录并运行 Antigravity；token 总量从本地会话数据库读取。
- Codex usage uses the local `%USERPROFILE%\.codex` state database, session files, and signed-in auth data.
  Codex 用量从 `%USERPROFILE%\.codex` 下的状态数据库、会话文件和本机登录信息读取。

## Build and publish / 构建与发布

```powershell
dotnet test tests\AiUsageWidget.Tests\AiUsageWidget.Tests.csproj
dotnet build src\AiUsageWidget\AiUsageWidget.csproj -c Release
.\publish.ps1
```

The publish script creates `publish\AiUsageWidget.exe` as a win-x64 single-file app that uses the installed .NET Desktop Runtime. Keep `悬浮球.ico` beside the executable so the custom window and tray icon can load.

发布脚本生成 `publish\AiUsageWidget.exe`。程序为 win-x64 单文件，并使用系统已安装的 .NET Desktop Runtime。请将 `悬浮球.ico` 与 exe 放在一起，供窗口和托盘加载自定义图标。

## Tooltip layout / 悬停详情格式

Chinese mode uses the following compact layout; English mode uses the same grouping with English labels.

中文模式采用以下紧凑格式；英文模式保持相同分组并使用英文标签。

```text
Gemini [5h : 92.8%] [周 : 33.6%]
Claude [5h : 100%] [周 : 19.1%]
用量 : 7.1M [昨日:125.6M  7天:169.4M  30天1175.5M]
重置 : 09-23 15:21:00 [余1h]   Week :09-24 10:15:00 [余20h]

Codex [5h : 95%] [周 : 23%]
用量 : 29.1M [昨日:73.7M, 7天:169.4M, 30天1175.5M]
重置 : 09-23 15:54:00 [余1h]   Week :09-27 09:29:00 [余91h]

2026-09-23 13:59:25
```

## Data and privacy / 数据与隐私

- Antigravity quota is read in read-only mode from its local language server. Its token totals are calculated from local conversation databases using local calendar-day boundaries.
  Antigravity 配额以只读方式从本机 language server 获取；token 总量从本机会话数据库读取，并按本地自然日边界统计。
- Codex token totals are calculated from local Codex session records. Five-hour and weekly quota data use the signed-in Codex account's usage endpoint.
  Codex token 总量根据本机 session 记录统计；五小时和周额度通过已登录 Codex 账户的用量接口读取。
- Credentials are used only for the local request and are not displayed, logged, or uploaded by this app.
  凭据只用于本机发起请求，不由本程序显示、写入日志或上传。
- Settings and error logs are stored separately under `%LOCALAPPDATA%\AiUsageWidget`.
  设置和错误日志独立保存在 `%LOCALAPPDATA%\AiUsageWidget`。

## Project layout / 项目结构

- `src\AiUsageWidget`: combined desktop app / 合并后的桌面程序
- `src\AiUsageWidget\Codex`: Codex reader and usage calculations / Codex 读取与统计模块
- `tests\AiUsageWidget.Tests`: unit and integration tests / 单元测试与集成测试

## License / 许可证

Released under the [MIT License](LICENSE).

本项目使用 [MIT License](LICENSE) 发布。
