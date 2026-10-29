# AiUsageWidget / AI 用量悬浮球

AiUsageWidget is a lightweight Windows floating widget for viewing Antigravity and Codex usage in one place. It combines local token totals with five-hour and weekly quota data, while the floating ball automatically follows the supported app currently in the foreground.

AiUsageWidget 是一个轻量的 Windows 用量悬浮球，在同一处展示 Antigravity 与 Codex 的本地 token 统计、五小时额度和周额度。悬浮球会根据当前位于前台的受支持应用自动切换数据来源。

## Features / 功能

- Floating center/fill shows the active provider's five-hour remaining percentage; the outer ring shows that provider's weekly remaining percentage.
  悬浮球中心和填充显示当前激活来源的五小时剩余比例，外环显示同一来源的周剩余比例。
- A compact, wide tooltip shows today's and yesterday's token totals for both providers, plus Gemini, Claude, and Codex quota rows.
  宽版紧凑悬停详情分别显示两种来源的今日和昨日 token 总量，并列出 Gemini、Claude 与 Codex 额度。
- Reset times and countdowns appear directly below each provider's usage row, followed by the latest data refresh timestamp.
  重置时间和剩余倒计时直接显示在对应来源的用量行下方，末尾显示最近的数据更新时间。
- Only the quota row represented by the floating ball is green. For Antigravity this is the currently selected Gemini or Claude model group; for Codex it is the Codex quota row. Every usage, reset, and timestamp row stays white.
  只有悬浮球当前代表的额度行显示为绿色：Antigravity 对应当前选中的 Gemini 或 Claude 模型组，Codex 对应 Codex 额度行；用量、重置和更新时间行均保持白色。
- Antigravity and Codex can each be enabled in settings. The tooltip shows an enabled provider only while its application is running; when one provider is shown, its quota row is green and the other rows are white.
  设置中可分别勾选 Antigravity 和 Codex。只有已勾选且对应程序正在运行时才显示其用量；只显示一个来源时，额度行显示绿色，其余行保持白色。
- Foreground ChatGPT/Codex selects Codex. Foreground Antigravity selects Antigravity. Other windows retain the last selection; startup defaults to Antigravity.
  前台为 ChatGPT/Codex 时选 Codex，前台为 Antigravity 时选 Antigravity；其他窗口保持最近一次选择，启动默认选择 Antigravity。
- Both providers are enabled by default. Codex presence includes either the Codex or ChatGPT desktop process.
  默认同时启用两个来源。检测 Codex 时，Codex 或 ChatGPT 桌面程序进程均视为正在运行。
- Chinese and English text follow the language configured in the widget.
  中英文显示完全跟随小工具中的语言设置。
- Tray controls, settings, startup behavior, multi-monitor placement, single-instance protection, and the current Antigravity icon are retained.
  保留托盘菜单、设置、开机启动、多显示器位置、单实例保护和当前 Antigravity 图标。
- The settings window groups display and ring controls alongside the daily-chat options. Existing legacy Codex token-budget values are retained when saving other settings.
  设置窗口将显示、外圈和每日会话选项分区排列；保存其他设置时会保留已有的旧版 Codex token 预算值。

## Daily temporary chat / 每日临时会话

In Settings, enable **Daily temporary chat**, set a local `HH:mm` time (default `05:30`), and independently select Codex and/or Antigravity. The feature is **off by default**; both apps are preselected. These choices are separate from the usage-display switches. **Test now** asks for confirmation and performs real requests using the current form without saving unrelated settings.

在设置中启用“每日临时会话”，设置电脑本地时间 `HH:mm`（默认 `05:30`），分别勾选 Codex、Antigravity。功能**默认关闭**，两软件初始均勾选，与“显示来源”互相独立。“立即测试”会确认后真实执行，使用当前表单选择，不会保存其他尚未保存的设置。

Time comes directly from the computer's local system clock/time zone (`TimeProvider.System`), not Tokyo time or an internet time service. “Tokyo” is only the weather-query location.

时间直接使用电脑系统的本地时间和时区（`TimeProvider.System`），不是东京时间，也不联网校时；“东京”仅指天气查询地点。

- Starts selected desktop apps if needed. Creates a new persistent background conversation in each app, sends `今日东京天气` once, then permanently deletes that exact conversation at least 60 seconds after send acknowledgement. There is no need to open the chat page. Normal model quota is consumed; deleting the chat does not refund usage.
  到点自动启动未运行的软件，各自后台新建独立会话发送“今日东京天气”，发送确认后至少等待60秒，再永久删除精确对应的会话，无需切到会话页面。会消耗正常模型额度，删除不会退还额度。
- Keep the widget running and the PC awake; apps must already be installed and signed in. This does not install Windows scheduled tasks, wake the PC, log in, or bypass permissions. A desktop app may come to the foreground when first launched. Local daylight/time-zone changes follow the PC clock. Missed minutes are not replayed, and each app is attempted at most once per local date, including failed attempts/restarts/time edits. A manual test does not consume the daily attempt.
  须保持悬浮球运行、电脑唤醒，目标软件已安装并登录。不创建 Windows 计划任务、不唤醒电脑、不代登录或绕过权限。首次启动软件可能切换到前台。按电脑当前本地时区运行，错过不补发；每软件每天最多自动尝试一次，失败、重启、修改时间也不重复发送；手动测试不占当天定时次数。
- Disabling stops new runs, **not pending cleanup**. Exiting cannot guarantee cleanup at the due time: the next widget launch recovers owned pending conversations without resending. Cleanup failure is retried at most three times per launch, 15 seconds apart; pending state remains for the next launch. Additional human messages, unknown identity, child-agent activity, or incomplete message history cause preservation, never guessed deletion. Check the per-app status in Settings.
  关闭开关只停止新执行，**仍会清理此前创建的会话**。退出后无法保证准时删除，下次启动会恢复已记录的待清理会话，不重发。清理失败每次启动最多重试三次，间隔15秒，失败记录保留至下次启动。人工追加消息、身份不明、子代理活动或记录不完整时保留会话，不猜测删除；设置中可查看结果。
- Uses Codex's configured default model with read-only/no-approval execution; Antigravity uses the default model returned by its own local service. It never changes provider model settings. Verified locally with Codex CLI `0.163.0-alpha.10.2` and `0.165.0-alpha.1.2` (create, send, wait, and delete), and Antigravity `2.27.0`; their local protocols may change in future versions. Model-response errors/quota failures can still occur after a message was accepted.
  Codex 使用其配置的默认模型，任务设为只读、不自动批准交互；Antigravity 使用本地服务返回的默认模型，不修改软件模型设置。已在 Codex CLI `0.163.0-alpha.10.2`、`0.165.0-alpha.1.2`（创建、发送、等待并删除）及 Antigravity `2.27.0` 上验证。**运行前检查版本，其他版本停止创建，需兼容性验证后更新小工具；待清理任务也会保留等待处理。**消息已接受后仍可能出现模型报错或额度不足。
- Durable ownership and daily attempts live in `%LOCALAPPDATA%\AiUsageWidget\conversation-runs.json`, separate from `settings.json`. Do not remove/edit that file while tasks are pending: clearing it loses deduplication and cleanup ownership. A corrupt/unwritable history stops all automatic actions; inspect/recover it and manually clean only the identified temporary chats before restarting. Credentials and message bodies are never written to this history.
  每日去重和会话所有权独立保存在 `%LOCALAPPDATA%\AiUsageWidget\conversation-runs.json`。有待清理任务时不要删除或修改它，否则会丢失去重和清理依据。执行记录损坏或不可写时停止所有自动操作；请检查恢复记录、只手动处理已确认的临时会话，再重启。记录不包含凭据或消息全文。

Only the verified versions above are enabled by the runtime compatibility guard; other versions stop before creation and leave pending cleanup for recovery/manual handling. History is rechecked immediately before deletion, but the external protocols provide no atomic conditional-delete transaction. Do not reuse these temporary chats for other work while cleanup is pending.

Codex background transport explicitly uses UTF-8, including on Chinese Windows installations. If a thread is no longer loaded, cleanup checks all active and archived list pages before treating it as already deleted. Cleanup diagnostics store only error types, RPC codes and method names, never remote error bodies or conversation text.

Codex 后台通信显式使用 UTF-8，避免中文 Windows 编码导致响应解析失败。对于未加载的会话，清理会核对完整的活动与归档列表，确认不存在后记为已删除。清理诊断只记录错误类型、接口错误码和方法名，不记录远端错误正文或会话内容。

删除前会再次检查消息，但外部接口不支持“检查消息与删除”原子事务；清理期间请勿复用这些临时会话处理其他工作。

An explicit opt-in integration harness is available (not part of unit tests):
独立的真实集成验证工具如下（不会随单元测试运行，会实际发送消息并删除新建测试会话）：

```powershell
dotnet run --project tests\AiUsageWidget.Smoke -- --live-create-send-delete <absolute-ownership-json-path>
```

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
用量 : 7.1M [昨日:125.6M]
重置 : 09-23 15:21:00 [余1h]   Week :09-24 10:15:00 [余20h]

Codex [5h : 95%] [周 : 23%]
用量 : 29.1M [昨日:73.7M]
重置 : 09-23 15:54:00 [余1h]   Week :09-27 09:29:00 [余91h]

2026-09-23 13:59:25
```

When only one provider is visible, its quota and usage are presented as a larger single-provider card. Its quota row is green and the other rows are white.

只显示一个来源时，会使用字号更大的单来源布局，额度行显示绿色，其余行保持白色。

```text
Codex : [5h:99%] [周:4%]
用量 : 3.2M [昨日:56.6M]
重置 :
5H   : 09-24 14:21:14 [余3h]
Week : 09-27 09:29:54 [余70h]

2026-09-24 10:30:35
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
