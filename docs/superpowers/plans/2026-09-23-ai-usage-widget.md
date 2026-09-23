# AiUsageWidget Implementation Plan

**Goal:** Create a standalone combined Codex and Antigravity usage widget at `D:\VS\AiUsageWidget`.

**Architecture:** Start from the current Antigravity WPF application and rebrand its namespaces and persisted app identity to AiUsageWidget. Compile the Codex data reader and usage refresh code into the same app, then select the floating-ball percentages from the foreground provider while formatting both providers in the tooltip.

**Tech Stack:** .NET 9 Windows, WPF, Windows Forms tray icon, Microsoft.Data.Sqlite, System.Management.

**Spec:** `docs/superpowers/specs/2026-09-23-ai-usage-widget-design.md`

## Global Constraints

- Target path is `D:\VS\AiUsageWidget`.
- Target is .NET 9 Windows/WPF and publishes as a self-contained-disabled, single-file win-x64 app.
- Settings, mutex, autostart identity, and log paths use `AiUsageWidget`.
- Codex and Antigravity usage remain separately attributed in the tooltip.
- Foreground `ChatGPT` selects Codex; foreground `Antigravity` selects Antigravity; any other process preserves the last provider; startup defaults to Antigravity.
- Do not modify either source project.

## Review Focus

- An unavailable provider must not inherit the other provider's percentage; inspect the independent null/fallback paths in the combined refresh state.
- Foreground lookup may fail or return the widget itself; the selector must preserve the prior provider in those cases.
- English and Chinese labels must come exclusively from the saved language setting.
- Codex credentials must not be included in status text, error logs, or published files.
- The selected Antigravity model and the original changed icon/lifecycle behavior must survive rebranding.

### Task 1: Establish the standalone project

**Files:**
- Create: `src/AiUsageWidget/` by copying `src/AntigravityUsageWidget/` including current working-tree changes and icon.
- Create: `tests/AiUsageWidget.Tests/` by copying the current Antigravity tests.
- Create: `src/AiUsageWidget/AiUsageWidget.csproj`, `src/AiUsageWidget.sln`, and `tests/AiUsageWidget.Tests/AiUsageWidget.Tests.csproj` by rebranding copied project metadata.
- Modify: copied namespaces, application title, startup value, mutex, settings directory, and log directory to use AiUsageWidget.
- Create: `README.md` and `publish.ps1` for the combined app.

**Interfaces:** The app remains a .NET 9 WPF executable; the copied test project references only `AiUsageWidget.csproj`.

- [x] Copy the Antigravity `src` and `tests` trees into the empty target directory, excluding `.git`, `publish`, `bin`, and `obj`.
- [x] Rename the copied application and test folders and replace `AntigravityUsageWidget` namespaces/project identities in those copied trees.
- [x] Change persisted app identifiers and displayed app title to `AiUsageWidget`, retaining the existing provider name where it identifies Antigravity data.
- [x] Add target-specific single-file publish script and bilingual README.
- [x] Build the copied app and test project to catch rebranding or XAML compilation errors.

### Task 2: Integrate Codex usage sources

**Files:**
- Create under `src/AiUsageWidget/Codex/`: `Data/CodexDataPaths.cs`, `Data/CodexDataReader.cs`, `Data/UsageJsonParser.cs`, `Data/OfficialUsageApiReader.cs`, `Data/OfficialUsageApiParser.cs`, `Data/OfficialUsageSnapshot.cs`, `Models/LocalRateLimitSnapshot.cs`, `Models/UsageRecord.cs`, `Models/UsageSnapshot.cs`, `Models/WidgetSettings.cs`, `Models/WidgetViewState.cs`, `Services/UsageCalculator.cs`, `Services/UsageRateCalculator.cs`, and `Services/UsageRefreshService.cs` from `D:\VS\codex_tools`.
- Modify: `src/AiUsageWidget/AiUsageWidget.csproj` to include the existing SQLite dependency.
- Create: `src/AiUsageWidget/Services/CodexUsageProvider.cs` to own the Codex refresh service and cached state.

**Interfaces:** `CodexUsageProvider.Refresh(now, settings, refreshOfficial)` returns `CodexUsageWidget.Models.WidgetViewState`.

- [x] Copy only the Codex reader, parser, models, and refresh-calculation files required by `UsageRefreshService` into the target project.
- [x] Add `CodexUsageProvider` using `CodexDataPaths.ForCurrentUser`, `CodexDataReader`, and `OfficialUsageApiReader`.
- [x] Convert saved AiUsageWidget settings into Codex `WidgetSettings` without changing user-visible AiUsageWidget settings.
- [x] Build the solution to catch copied source dependency or namespace errors.

### Task 3: Select active percentages and show both providers

**Files:**
- Create: `src/AiUsageWidget/Services/ForegroundProcessReader.cs`.
- Create: `src/AiUsageWidget/Services/UsageProviderSelector.cs`.
- Create: `src/AiUsageWidget/Services/AiUsageTooltipFormatter.cs`.
- Modify: `src/AiUsageWidget/MainWindow.xaml.cs` to own both refresh services, poll the foreground process, and update the ball from the selected provider.

**Interfaces:** `UsageProviderSelector.Select(foregroundProcessName, previousProvider)` returns `UsageProvider`; `AiUsageTooltipFormatter.Format(antigravityState, codexState, refreshedAt, english)` returns tooltip detail text.

- [x] Implement case-insensitive foreground mapping for `ChatGPT` and `Antigravity`, retaining the previous source for other names and defaulting to Antigravity.
- [x] Add a one-second lightweight provider-selection timer; do not force official network refresh on each tick.
- [x] Keep both providers refreshed on the existing usage refresh cycle and retain their last successful states independently.
- [x] Feed active provider five-hour percentage to the ball center/fill and its weekly percentage to the weekly ring.
- [x] Format separate Codex and Antigravity today/yesterday token totals, all provider quota rows, update time, and labeled reset times according to the configured language.
- [x] Build the Release application and publish the win-x64 single-file output.

### Task 4: Check the delivered project surface

**Files:**
- Modify: `README.md`, `.gitignore`, and `publish.ps1` if needed.

**Interfaces:** The project README and publish script use only target-relative paths.

- [x] Inspect the target tree and Git status to confirm no source-project files were modified and the copied icon is present.
- [x] Run the Release build and publish commands from the target project and confirm `publish/AiUsageWidget.exe` is produced.
- [x] Review bilingual README instructions and confirm they name both data sources and the foreground-switch rule.
- [x] Record build/publish results and any unverified runtime behavior in the handoff.
