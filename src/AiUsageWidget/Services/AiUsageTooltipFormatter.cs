using System.Globalization;
using AiUsageWidget.Models;
using AiUsageWidget.Data;
using CodexWidgetViewState = CodexUsageWidget.Models.WidgetViewState;

namespace AiUsageWidget.Services;

public static class AiUsageTooltipFormatter
{
    public static string FormatDetails(
        WidgetViewState? antigravity,
        CodexWidgetViewState? codex,
        DateTimeOffset refreshedAt,
        bool english)
    {
        var lines = new List<string>();
        if (antigravity is not null)
        {
            lines.Add(FormatTokenUsage("Antigravity", antigravity.TodayTokens, antigravity.YesterdayTokens, english));
            AddAntigravityQuotaLines(lines, antigravity, english);
        }

        if (codex is not null)
        {
            lines.Add(FormatTokenUsage("Codex", codex.TodayTokens, codex.YesterdayTokens, english));
            lines.Add(english
                ? $"Codex last 7 days:{FormatMillions(codex.SevenDay.Usage.TotalTokens)}"
                : $"Codex 近7天总量:{FormatMillions(codex.SevenDay.Usage.TotalTokens)}");
            lines.Add(english
                ? $"Codex last 30 days:{FormatMillions(codex.ThirtyDay.Usage.TotalTokens)}"
                : $"Codex 近30天总量:{FormatMillions(codex.ThirtyDay.Usage.TotalTokens)}");
            lines.Add(FormatQuotaLine(
                "Codex",
                codex.FiveHourRemainingPercent,
                codex.OfficialRemainingPercent,
                english));
        }

        lines.Add(string.Empty);
        lines.Add($"{(english ? "Updated" : "更新时间")}:{refreshedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        return string.Join(Environment.NewLine, lines);
    }

    public static string? FormatResetDetails(
        WidgetViewState? antigravity,
        CodexWidgetViewState? codex,
        DateTimeOffset now,
        bool english)
    {
        var lines = new List<string>();
        if (antigravity is not null)
        {
            AddReset(lines, "Antigravity", antigravity.FiveHourResetAt, now, english, isWeekly: false);
            AddReset(
                lines,
                "Antigravity",
                antigravity.WeeklyResetAt ?? antigravity.ResetAt,
                now,
                english,
                isWeekly: true);
        }

        if (codex is not null)
        {
            AddReset(lines, "Codex", codex.FiveHourResetAt, now, english, isWeekly: false);
            AddReset(
                lines,
                "Codex",
                codex.WeeklyResetAt ?? codex.ResetAt,
                now,
                english,
                isWeekly: true);
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static void AddAntigravityQuotaLines(
        ICollection<string> lines,
        WidgetViewState state,
        bool english)
    {
        var quota = state.Quota;
        if (quota is null)
        {
            lines.Add(FormatQuotaLine(
                english ? "Antigravity" : "Antigravity",
                state.FiveHourRemainingPercent,
                state.OfficialRemainingPercent,
                english));
            return;
        }

        var groups = quota.Rows
            .GroupBy(row => row.Group ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (groups.Length == 0)
        {
            lines.Add(FormatQuotaLine(
                english ? "Models" : "模型",
                quota.ShortRemainingPercent,
                quota.WeeklyRemainingPercent,
                english));
            return;
        }

        foreach (var group in groups)
        {
            var shortPercent = group
                .Where(row => row.Period == AntigravityQuotaPeriod.Short)
                .Select(row => (double?)row.RemainingPercent)
                .OrderBy(value => value)
                .FirstOrDefault();
            var weeklyPercent = group
                .Where(row => row.Period == AntigravityQuotaPeriod.Weekly)
                .Select(row => (double?)row.RemainingPercent)
                .OrderBy(value => value)
                .FirstOrDefault();
            lines.Add(FormatQuotaLine(FormatGroupName(group.Key, english), shortPercent, weeklyPercent, english));
        }
    }

    private static string FormatGroupName(string group, bool english)
    {
        if (group.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemini";
        }

        if (group.Contains("Claude", StringComparison.OrdinalIgnoreCase) ||
            group.Contains("GPT", StringComparison.OrdinalIgnoreCase))
        {
            return "Claude";
        }

        return string.IsNullOrWhiteSpace(group)
            ? english ? "Models" : "模型"
            : group.Trim();
    }

    private static string FormatTokenUsage(string name, long today, long yesterday, bool english)
    {
        return english
            ? $"{name} tokens today:{FormatMillions(today)} (yesterday:{FormatMillions(yesterday)})"
            : $"{name} 今日token:{FormatMillions(today)}(昨日：{FormatMillions(yesterday)})";
    }

    private static string FormatQuotaLine(string name, double? fiveHour, double? weekly, bool english)
    {
        var colon = english ? ":" : "：";
        var weeklyLabel = english ? "Weekly" : "周";
        return $"{name} : [5h{colon}{FormatPercent(fiveHour, english)}][{weeklyLabel}{colon}{FormatPercent(weekly, english)}]";
    }

    private static string FormatPercent(double? value, bool english)
    {
        return value.HasValue
            ? $"{Math.Clamp(value.Value, 0, 100).ToString("0.#", CultureInfo.InvariantCulture)}%"
            : english ? "unavailable" : "不可用";
    }

    private static string FormatMillions(long tokens)
    {
        return $"{(Math.Max(0, tokens) / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture)}M";
    }

    private static void AddReset(
        ICollection<string> lines,
        string provider,
        DateTimeOffset? resetAt,
        DateTimeOffset now,
        bool english,
        bool isWeekly)
    {
        if (!resetAt.HasValue)
        {
            return;
        }

        var hours = Math.Max(0, Math.Floor((resetAt.Value - now).TotalHours))
            .ToString("0", CultureInfo.InvariantCulture);
        var label = english
            ? $"{provider} {(isWeekly ? "Weekly" : "5-hour")} reset"
            : $"{provider}{(isWeekly ? "周" : "五小时")}重置时间";
        lines.Add(english
            ? $"{label}: {resetAt.Value.ToLocalTime():yyyy-MM-dd HH:mm} [{hours}h remaining]"
            : $"{label}:{resetAt.Value.ToLocalTime():yyyy-MM-dd HH:mm} [剩余 {hours}h]");
    }
}
