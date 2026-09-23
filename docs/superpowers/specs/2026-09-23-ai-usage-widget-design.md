# AiUsageWidget 设计说明

## 目标

基于当前 AntigravityUsageWidget 创建独立的 Windows 桌面项目 `AiUsageWidget`，并合入 CodexUsageWidget 的额度与 token 统计。原有两个目录及其本地 Git 状态保持不变。

## 用户可见行为

- 保留当前 Antigravity 悬浮球、托盘菜单、设置、语言切换、重置倒计时、每日 token 统计及模型分组额度。
- 悬停详情同时显示 Antigravity 的 Gemini/Claude 配额和 Codex 配额；Codex 行格式为 `Codex : [5h：100%][周：50%]`。英文界面使用设置中的语言显示标签。
- 悬浮球中心和五小时填充显示激活应用的五小时剩余比例，周环显示同一应用的周剩余比例。
- 通过 Windows 前台窗口所属进程切换来源：`ChatGPT`/`Codex` 选择 Codex，`Antigravity` 选择 Antigravity。前台是其他应用时维持最近一次选择；启动时默认 Antigravity，若启动时前台已经是上述应用则立即选中它。
- Codex 与 Antigravity 的今日/昨日 token 统计分别显示，避免混淆来源；各自的额度和重置时间也明确归属。
- 设置中保留可选的 Codex 周 token 预算，用于 Codex 本地统计估算；留空时禁用该预算。
- 未能读取某来源时保留其最近一次成功额度；首次不可用则显示“不可用”，不把另一个来源的数字伪装成该来源额度。

## 数据与组件

- Antigravity 数据继续从本机 language server 与本机对话数据库读取，并沿用当前选中模型分组逻辑。
- Codex 数据从当前用户 `.codex` 状态数据库和 sessions 记录读取；五小时/周官方额度从 Codex 已登录账户读取。token、account id 等认证材料只用于本机请求，不进入显示、日志或上传。
- 新增一个轻量前台进程读取器和额度来源选择器。选择器只接收进程名并返回当前额度来源，方便无窗口环境下确定行为。
- 新增统一悬停格式化器，按当前语言设置输出两来源 token、额度、更新时间与重置时间。
- 两个读取器独立保留最近一次成功状态；前台切换只改变悬浮球所用状态，不改变悬停详情的双来源展示。

## 工程约束

- 目标位置：`D:\VS\AiUsageWidget`，.NET 9 Windows/WPF，x64 单文件发布，依赖已安装的 .NET Desktop Runtime。
- Antigravity 项目作为外壳并保留其当前工作树已有改动与图标；Codex 核心读取器源码编入新项目，避免构建依赖原项目目录。
- 用户设置、单实例互斥锁、自动启动注册值和错误日志均使用 `AiUsageWidget` 独立名称，不覆盖任一旧应用的设置。
- README 提供中文和英文说明、运行前提、构建与发布命令、数据来源和隐私说明。

## 不在范围内

- 不修改或提交 `D:\VS\codex_tools`、`D:\VS\antigravity_tools` 中的现有改动。
- 不把两边 token 合计成一个不可区分的数字。
- 不创建或修改 GitHub 仓库；本轮先完成目标目录中的本地项目。
