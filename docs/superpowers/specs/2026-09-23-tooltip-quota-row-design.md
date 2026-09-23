# Tooltip Quota Row Design

## Goal

Replace the existing provider-header tooltip with compact quota, usage, and reset rows. Only the quota row represented by the floating ball is green.

## Chinese layout

```text
Gemini [5h : 69%] [周 : 13%]
Claude [5h : 69%] [周 : 13%]
用量 : 12.2M [昨日:79.6M  7天:324.7M  30天732.9M]
重置 : 09-23 20:21:08 [余3h]   Week :09-27 09:29:53 [余88h]

Codex [5h : 69%] [周 : 13%]
用量 : 46.9M [昨日:73.7M, 7天:187.2M, 30天1190.9M]
重置 : 09-23 20:21:08 [余3h]   Week :09-27 09:29:53 [余88h]

2026-09-23 17:12:48
```

## Highlighting

- When the floating ball displays Antigravity, highlight only the currently selected Antigravity model group quota row: `Gemini [...]` or `Claude [...]`.
- When the floating ball displays Codex, highlight only the `Codex [...]` quota row.
- Usage, reset, blank, and update-time rows are always white.
- If the selected Antigravity model group cannot be identified, neither Gemini nor Claude is highlighted.

## Data behavior

- Preserve the selected Antigravity model group in the aggregated quota state.
- An exact selected model ID takes precedence; otherwise map the selected model label to Gemini or Claude/GPT.
- Reset countdowns use the current time, while the final timestamp is the latest successful provider refresh time.
- Keep Chinese and English modes; English uses equivalent labels with the same row structure.

## Delivery

- Update automated tests and the bilingual README.
- Run the complete test suite and Release build.
- Republish `publish/AiUsageWidget.exe`, restart the app, commit, and push the tracked GitHub branch.
