# Daily Temporary Conversation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在悬浮球运行期间，每日定时在所选软件新建可见会话、发送“今日东京天气”，发送成功 60 秒后精确删除该临时会话。

**Architecture:** 独立调度状态机依赖可注入的 `TimeProvider`、原子 JSON 执行记录和每软件一个会话适配器。WPF 只管理设置、状态显示和异步生命周期；应用接口兼容性验证先于功能交付，不能用无效适配器或模糊 UI 删除兜底。

**Tech Stack:** Existing C#/.NET 9 Windows WPF, System.Text.Json, xUnit; use existing local signed-in apps, no new cloud API account.

**Spec:** `docs/superpowers/specs/2026-10-28-daily-conversation-design.md`

## Global Constraints

- 默认时间 `05:30`，电脑本地时区；总开关升级默认关闭，Codex/Antigravity 初始均勾选且与用量显示开关独立。
- 未运行软件自动启动；发送固定消息“今日东京天气”，成功后至少等待 60 秒，不等待回答完成。
- 目标分钟内触发，每软件每自然日最多自动尝试一次；错过不补发，修改时间不立刻补跑。
- 关闭只阻止新创建；已有待清理会话仍清理，重启后恢复清理而不重发。
- 仅删除已持久化为本工具创建的精确会话 ID；人工追加消息、身份不明、记录损坏时禁止删除。
- 不使用全局键盘/剪贴板发送，不修改对方数据库，不以归档冒充永久删除，不绕过登录/权限/锁屏。
- 中英双语；仅本地存储必要状态，不记录凭据或完整会话内容。不改用量统计。

## Review Focus

1. 软件升级或接口缺失：在创建前尽可能检查完整能力，不把假成功交给用户（Task 1/4）。
2. 写盘失败或请求结果不明：禁止重发或猜测清理，明确记录不确定状态（Task 2/3）。
3. 夏令时、时区变更、时钟回拨及重启：同一本地日期/软件不能自动重复，错过不补发（Task 3）。
4. 人工接管临时会话或生成中删除：检查用户消息、停止同 ID 的生成，不能删除人工内容（Task 4）。
5. 关闭设置、退出时在途操作、低分辨率窗口：清理可恢复，控件和状态不可遮挡保存按钮（Task 3/5）。

---

### Task 1: Verify complete app integration contracts before writing adapters

**Files:** Create `docs/superpowers/plans/2026-10-28-conversation-protocol-notes.md`; no product changes in this task.

**Interfaces:** Produces verified method/CLI names, request/response shapes and app URI/launch rules for Task 4. The notes must distinguish observed behavior from assumptions and contain no credentials or personal message contents.

- [x] Read installed Codex CLI/app-server schemas and official documentation. Verify create/persist/open/send/read/interrupt/delete by exact ID; preserve app defaults and select read-only/no-approval execution for the weather request.
- [x] Inspect installed Antigravity local service and deep-link contracts read-only. Identify create/open/send/read/stop/delete operations and how its currently selected/default model is retained. Do not enable remote debugging or change app startup flags just to gain access.
- [x] Run one dedicated temporary conversation per app, explicitly track the new ID, send the fixed prompt once, check visibility, wait at least 60 seconds, stop if generating and delete only that ID. Never use current/latest-thread selectors. Record command/API confirmation without existing conversation contents.
- [x] Verify manual takeover can be detected through the app's own message reads; verify missing/deleted ID behavior and whether delete has descendant or project side effects.
- [x] If either complete safe chain is unavailable, stop before enabling a partial feature. Report the specific missing capability and request direction; do not silently switch to archiving, background-only requests, or keyboard automation.
- [x] Commit only the verified non-secret protocol notes: `git add -- docs/superpowers/plans/2026-10-28-conversation-protocol-notes.md` then `git commit -m "docs: verify local temporary conversation protocols"`.

### Task 2: Settings and durable run ownership

**Files:** Modify `src/AiUsageWidget/Models/WidgetSettings.cs`, `src/AiUsageWidget/Services/SettingsStore.cs`, `tests/AiUsageWidget.Tests/SettingsStoreTests.cs`; create `src/AiUsageWidget/Scheduling/ConversationModels.cs`, `ConversationRunStore.cs`, `tests/AiUsageWidget.Tests/ConversationRunStoreTests.cs`.

**Interfaces:**
- Settings add `bool DailyConversationEnabled = false`, `string DailyConversationTime = "05:30"`, `bool DailyConversationCodex = true`, `bool DailyConversationAntigravity = true` as init properties.
- `ConversationApp` enum: `Codex`, `Antigravity`. `CreatedConversation` record: `ConversationApp App, string ThreadId, Guid RunId, DateTimeOffset CreatedAt`.
- `ConversationRun` record includes `RunId`, `App`, nullable `DateOnly ScheduledDate` (null for manual test), stage, optional `CreatedConversation`, `SendStartedAt`, `SentAt`, `CleanupDueAt`, retry count/time, and a sanitized result code. Stages: Reserved, Created, Sending, CleanupPending, Completed, Failed, Preserved.
- `IConversationRunStore.Load(): IReadOnlyList<ConversationRun>` and `Save(IReadOnlyList<ConversationRun> runs): void`; `ConversationRunStore(string? directory = null)` implements versioned atomic JSON storage separate from settings. Load failure is distinguishable from an empty history and disables new scheduling until resolved, not silently erased.

- [x] Write `SettingsStoreTests` asserting old JSON defaults to disabled/05:30/both selected; round-trip exact values; invalid time normalizes to05:30, valid time canonicalizes toHH:mm; enabled/no provider is rejected by UI rather than silently enabling one.
- [x] Write `ConversationRunStoreTests` for round-trip ownership/dates/stages, malformed/unsupported schema, invalid ID/app/run identity, atomic-write failure preserving previous file, no secrets in serialized result fields. Corrupt state must never produce a deletable run.
- [x] Run `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj --filter "FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~ConversationRunStoreTests"`; observe missing-property/type failures before implementation.
- [x] Implement the settings, models and store interfaces above. Use a task-specific temp filename and atomic move, matching `SettingsStore`; retain history for duplicate prevention rather than overwriting it with only the latest run.
- [x] Run the same filtered tests; require zero failures, then `git diff --check` and commit the exact changed files with `feat: persist daily conversation settings and ownership`.

### Task 3: Test-driven scheduler and cleanup state machine

**Files:** Create `src/AiUsageWidget/Scheduling/IConversationAdapter.cs`, `DailyConversationScheduler.cs`, `tests/AiUsageWidget.Tests/DailyConversationSchedulerTests.cs`.

**Interfaces:**
- `IConversationAdapter`: `ConversationApp App { get; }`; `Task EnsureReadyAsync(CancellationToken ct)`; `Task<CreatedConversation> CreateAndOpenAsync(Guid runId, CancellationToken ct)`; `Task SendAsync(CreatedConversation conversation, string text, CancellationToken ct)`; `Task<bool> CanSafelyDeleteAsync(CreatedConversation conversation, CancellationToken ct)`; `Task StopAndDeleteAsync(CreatedConversation conversation, CancellationToken ct)`.
- `DailyConversationScheduler(IConversationRunStore store, IEnumerable<IConversationAdapter> adapters, TimeProvider clock)`; `Task TickAsync(WidgetSettings settings, CancellationToken ct)`; `Task TestNowAsync(WidgetSettings settings, CancellationToken ct)`; immutable per-app status property and status-changed event for Task 5. Tick processes cleanup even if disabled. Use the per-app in-flight guard for both entry points, shared store serialization, and bounded cancellation.

- [x] Write fake-clock/fake-adapter tests: `DisabledDoesNotCreate`, `OnlySelectedAppsRun`, `DueMinuteRunsEachAppOnce`, `MissedMinuteDoesNotRun`, `RestartDoesNotRepeat`, `TimeChangeDoesNotRunImmediately`, `ConcurrentTicksDoNotDuplicate`, `OneFailureDoesNotBlockOtherApp`, `ManualTestDoesNotConsumeDailyRun`.
- [x] Add safety/recovery tests: `ReserveWriteFailureDoesNotLaunch`, `OwnershipWriteFailureDoesNotSend`, `AmbiguousSendDoesNotResend`, `DoesNotDeleteBeforeSixtySeconds`, `DisabledStillCleansOwnedRun`, `RestartResumesCleanupNotSending`, `ForeignIdIsNeverDeleted`, `HumanTakeoverPreservesConversation`, `FailedCleanupRemainsPending`, `CorruptStoreStopsScheduling`, `CancellationRetainsCleanupRecord`, `ClockRollbackDoesNotDuplicateDate`, `TimezoneChangeDoesNotDuplicateLocalDate`.
- [x] Run `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj --filter FullyQualifiedName~DailyConversationSchedulerTests`; require red before writing the scheduler.
- [x] Implement the state machine with the Task 2 models. Reserve a run before external action; persist identity before sending; persist Sending before the non-idempotent send. Never retry ambiguous sends. Persist the cleanup deadline after send confirmation; for failed/uncertain sends retain the known ID for safe cleanup no earlier than60s after the attempt ended. A returned-but-unpersistable ID is reported as an orphan risk, never used for guessed future cleanup.
- [x] Check ownership and no human takeover immediately before stopping/deleting. Use at most3 cleanup attempts per runtime cycle with15s backoff, keep pending records after exhaustion, and permit a new bounded recovery cycle on next widget launch. Do not open a second conversation while one of the same app remains in flight.
- [x] Run the scheduler/store tests then the full test project; require zero failures, `git diff --check`, commit `feat: schedule and safely recover daily temporary conversations`.

### Task 4: Real Codex and Antigravity adapters

**Files:** Create `src/AiUsageWidget/Scheduling/CodexConversationAdapter.cs`, `AntigravityConversationAdapter.cs`, `DesktopAppLauncher.cs`, any narrowly scoped transport helper justified by Task 1; create `tests/AiUsageWidget.Tests/ConversationAdapterTests.cs`.

**Interfaces:** Both adapters implement Task 3's `IConversationAdapter`. `DesktopAppLauncher.EnsureStartedAsync(ConversationApp app, CancellationToken ct): Task` discovers installed executables/registered protocols without changing machine configuration. Separate transport/process factories must be injectable for tests, no live model requests in unit tests.

- [x] Write adapter tests using the verified Task 1 protocol shapes: require correct app/exact returned ID at every operation; failed auth/capability/startup/invalid JSON/timeout cannot report success; extra human messages return false for safe deletion; no extra user messages and matching owner return true; no background-only success; already missing IDs are idempotent delete completion.
- [x] Add tests proving requests use the fixed prompt once, do not override the model with a hardcoded name, do not bypass permissions, and redact auth/response contents from logged errors. Include generating-turn interruption and descendant-delete side-effect rejection.
- [x] Run `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj --filter FullyQualifiedName~ConversationAdapterTests` and observe red.
- [x] Implement only the verified Task 1 operations with timeouts (startup60s, ordinary RPC15s), bounded body sizes, loopback-only local endpoints, narrowly scoped local TLS handling where required, process argument lists (no shell-built commands), no foreground/global-input fallbacks.
- [x] Run adapter tests, then repeat the dedicated live smoke flow for each installed app including preserved manual-takeover test. Track each newly created ID explicitly; keep a takeover test conversation rather than deleting human-added content. If live behavior differs, fix/tests first or stop with the concrete compatibility blocker.
- [x] Run the full test project and `git diff --check`; commit `feat: integrate verified desktop conversation adapters` only after successful live evidence.

### Task 5: WPF settings, lifecycle, documentation and release verification

**Files:** Modify `src/AiUsageWidget/MainWindow.xaml`, `MainWindow.xaml.cs`, `tests/AiUsageWidget.Tests/SettingsWindowLayoutTests.cs`, `README.md`; create `src/AiUsageWidget/MainWindow.Scheduling.cs`, `tests/AiUsageWidget.Tests/ConversationSettingsTests.cs`.

**Interfaces:** The partial MainWindow file owns a scheduler timer (5s), cancellation lifetime, status-event UI marshaling, `TestDailyConversation_Click`, settings validation and bilingual scheduling labels. It consumes Tasks 2–4, avoiding a large unrelated MainWindow refactor.

- [x] Write validation tests: validHH:mm, invalid/missing time, enabled/zero selected, disabled/zero selected, scheduling options independent of ShowCodex/ShowAntigravity. Add layout checks for scrollable settings content with fixed accessible action buttons; manually verify at100%/150% scaling.
- [x] Run the filtered settings/layout tests and observe new assertions fail before modifying UI.
- [x] Add the independent scheduling settings group, next-run/per-app result labels, foreground/use-cost/cleanup-after-disable notice and explicit confirmation for the real immediate test. Validate the current form before testing without silently saving unrelated settings. Keep save/cancel usable.
- [x] Wire scheduler creation/start after window load, periodic asynchronous tick without reentrancy, saved settings updates, and graceful cancellation on close. Persisted cleanup remains recoverable if the user exits mid-run. Manual test uses selected apps and the same safe state machine; statuses follow widget language.
- [x] Document bilingual configuration, running/awake/logged-in prerequisites, default off, local time, skipped missed runs, real usage cost, permanent delete versus archive, manual takeover and cleanup recovery. Document supported installed versions from actual Task1/4 evidence, not assumed versions.
- [x] Run `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj`, `dotnet build src/AiUsageWidget/AiUsageWidget.csproj -c Release`, and `git diff --check`; require zero failures and successful build. Perform visual settings QA and use a near-future schedule to validate actual timer execution, both providers and daily dedup without editing system time.
- [x] Obtain independent whole-change review, fix safety or regression findings and rerun affected verification plus the full tests. Only then commit feature/docs, run `./publish.ps1` (after safely stopping only this widget instance if locked), verify resulting executable, and push the existing project branch per the user's established workflow. Do not claim full support if either app smoke flow is incomplete.

## Self-review

All design sections map to the five tasks; adapter feasibility is an explicit prerequisite, not an assumed implementation detail. Ownership/run-state types in Tasks2–4 use the same contract. Each of the five review-focus conditions is pinned to named tests/verification above. Unit tests never create/delete real user conversations; live tests are limited to exact newly created dedicated IDs.
