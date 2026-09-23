# Tooltip Quota Row Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render the confirmed compact tooltip and highlight only the quota row represented by the floating ball.

**Architecture:** Extend `AntigravityDisplayQuota` with an explicit selected group enum, then format quota, usage, reset, and update-time rows as one complete tooltip string. The presentation layer will use the active provider plus selected Antigravity group to mark exactly one quota row for WPF rendering.

**Tech Stack:** .NET 9, WPF, xUnit, PowerShell publish script.

**Spec:** `docs/superpowers/specs/2026-09-23-tooltip-quota-row-design.md`

## Global Constraints

- Chinese spacing and punctuation match the approved example exactly.
- Only the selected quota row is green; every non-quota row remains white.
- Unknown Antigravity selection highlights neither model row.
- Reset timestamps use `MM-dd HH:mm:ss`; the final update timestamp uses `yyyy-MM-dd HH:mm:ss`.
- Preserve English mode and current unavailable-data fallback behavior.

## Review Focus

- Selected model ID and label can disagree; an exact ID match must determine the highlighted group.
- A selected label may be absent or unfamiliar; no Antigravity quota row should be highlighted.
- Antigravity can have a group whose display name is Codex; only exact generated quota-row prefixes may trigger highlighting.
- One reset period can be absent; the remaining period must still be labeled and displayed.
- Provider switching must update colors without requiring a fresh quota read.

---

### Task 1: Preserve the selected Antigravity quota group

**Files:**
- Modify: `src/AiUsageWidget/Services/AntigravityQuotaAggregator.cs`
- Test: `tests/AiUsageWidget.Tests/AntigravityQuotaAggregatorTests.cs`
- Test: `tests/AiUsageWidget.Tests/AntigravityUsageRefreshServiceTests.cs`

**Interfaces:**
- Produces: `AntigravityQuotaGroup` and `AntigravityDisplayQuota.SelectedGroup`.
- Consumes: `AntigravityQuotaSnapshot.SelectedModelId`, `SelectedModelLabel`, and quota row groups.

- [ ] **Step 1: Write failing aggregation tests**

Add literal assertions that a Gemini label produces `Gemini`, a matching Claude model ID produces `Claude`, and an unrecognized selection produces `Unknown`.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj --filter "FullyQualifiedName~AntigravityQuotaAggregatorTests|FullyQualifiedName~AntigravityUsageRefreshServiceTests"`

Expected: compilation fails because `SelectedGroup` and `AntigravityQuotaGroup` do not exist.

- [ ] **Step 3: Implement selected-group propagation**

Add the enum and record property, derive the group from an exact selected-ID row before the label fallback, and retain `Unknown` when no supported group can be identified.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Step 2 command.

Expected: all focused tests pass.

### Task 2: Format and present the compact tooltip

**Files:**
- Modify: `src/AiUsageWidget/Services/AiUsageTooltipFormatter.cs`
- Modify: `src/AiUsageWidget/Services/AiUsageTooltipPresentation.cs`
- Modify: `src/AiUsageWidget/MainWindow.xaml.cs`
- Test: `tests/AiUsageWidget.Tests/AiUsageTooltipFormatterTests.cs`
- Test: `tests/AiUsageWidget.Tests/AiUsageTooltipPresentationTests.cs`
- Test: `tests/AiUsageWidget.Tests/MainWindowCompositionTests.cs`

**Interfaces:**
- Consumes: `AntigravityDisplayQuota.SelectedGroup` from Task 1.
- Produces: one complete formatted tooltip string and `AiUsageTooltipPresentation.Build(details, activeProvider, selectedGroup)`.

- [ ] **Step 1: Replace expectations with the approved layout and highlight rules**

Assert the exact Chinese string, equivalent English labels, inline reset rows, final update timestamp, Gemini-only/Claude-only/Codex-only highlighting, and white usage/reset/timestamp rows.

- [ ] **Step 2: Run the tooltip-focused tests and verify RED**

Run: `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj --filter "FullyQualifiedName~AiUsageTooltipFormatterTests|FullyQualifiedName~AiUsageTooltipPresentationTests|FullyQualifiedName~MainWindowCompositionTests"`

Expected: assertions or compilation fail because the formatter and presentation signatures still implement the old provider-block layout.

- [ ] **Step 3: Implement the formatter, presentation, and WPF wiring**

Format each provider block in the approved order, append the latest refresh timestamp, recompute the full text on countdown ticks, and pass the selected Antigravity group into the presentation layer. Highlight only exact quota rows.

- [ ] **Step 4: Run focused tests and the full suite**

Run the Step 2 command, then `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj`.

Expected: focused and complete suites pass with zero failures.

### Task 3: Document, build, publish, and deliver

**Files:**
- Modify: `README.md`
- Regenerate: `publish/AiUsageWidget.exe`

**Interfaces:**
- Consumes: the user-visible layout and highlight behavior implemented by Task 2.
- Produces: updated bilingual documentation and a runnable published executable.

- [ ] **Step 1: Update bilingual documentation**

Replace the tooltip example and explain that only the selected quota row is green.

- [ ] **Step 2: Verify tests and Release build**

Run: `dotnet test tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj`

Run: `dotnet build src/AiUsageWidget/AiUsageWidget.csproj -c Release --no-restore`

Expected: both commands exit 0 with zero test failures and zero build errors.

- [ ] **Step 3: Publish and verify the artifact**

Run: `.\publish.ps1`

Expected: `publish/AiUsageWidget.exe` exists and has a fresh modification time.

- [ ] **Step 4: Request whole-change code review**

Review the diff from the pre-change commit to `HEAD` against the spec, especially the five Review Focus cases.

- [ ] **Step 5: Commit, push, and restart**

Commit the implementation and documentation, push the tracked branch, restart the published executable, and verify the running process path is `D:\VS\AiUsageWidget\publish\AiUsageWidget.exe`.
