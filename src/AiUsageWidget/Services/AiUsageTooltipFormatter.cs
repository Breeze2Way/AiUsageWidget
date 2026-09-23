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
            lines.Add(FormatTokenUsage(
                "Antigravity",
                antigravity.TodayTokens,
                antigravity.YesterdayTokens,
                antigravity.SevenDayTokens,
                antigravity.ThirtyDayTokens,
                english));
            AddAntigravityQuotaLines(lines, antigravity, english);
        }

        if (codex is not null)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(FormatTokenUsage(
                "Codex",
                codex.TodayTokens,
                codex.YesterdayTokens,
                codex.SevenDay.Usage.TotalTokens,
                codex.ThirtyDay.Usage.TotalTokens,
                english));
            lines.Add("      " + FormatQuotaLine(
                "Codex",
                codex.FiveHourRemainingPercent,
                codex.OfficialRemainingPercent,
                english));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string? FormatResetDetails(
        WidgetViewState? antigravity,
        CodexWidgetViewState? codex,
        DateTimeOffset now,
        bool english)
    {
        var providerLines = new List<string>();
        if (antigravity is not null)
        {
            AddProviderReset(
                providerLines,
                "Antigravity",
                antigravity.FiveHourResetAt,
                antigravity.WeeklyResetAt ?? antigravity.ResetAt,
                now,
                english);
        }

        if (codex is not null)
        {
            AddProviderReset(
                providerLines,
                "Codex",
                codex.FiveHourResetAt,
                codex.WeeklyResetAt ?? codex.ResetAt,
                now,
                english);
        }

        if (providerLines.Count == 0)
        {
            return null;
        }

        var refreshedAt = LatestRefreshAt(antigravity, codex).ToLocalTime();
        var heading = english
            ? $"Reset times [updated {refreshedAt:yyyy-MM-dd HH:mm:ss}]:"
            : $"重置时间[{refreshedAt:yyyy-MM-dd HH:mm:ss}更新]:";
        providerLines.Insert(0, heading);
        return string.Join(Environment.NewLine, providerLines);
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
            lines.Add("     " + FormatQuotaLine(
                FormatGroupName(group.Key, english),
                shortPercent,
                weeklyPercent,
                english));
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

    private static string FormatTokenUsage(
        string name,
        long today,
        long yesterday,
        long sevenDay,
        long thirtyDay,
        bool english)
    {
        return english
            ? $"{name} : {FormatMillions(today)}[Yesterday:{FormatMillions(yesterday)}, " +
              $"7 days:{FormatMillions(sevenDay)}, 30 days:{FormatMillions(thirtyDay)}]"
            : $"{name} : {FormatMillions(today)}[昨日:{FormatMillions(yesterday)}，" +
              $"7天:{FormatMillions(sevenDay)}，30天{FormatMillions(thirtyDay)}]";
    }

    private static string FormatQuotaLine(string name, double? fiveHour, double? weekly, bool english)
    {
        var weeklyLabel = english ? "Week" : "周";
        return $"{name} : [5h:{FormatPercent(fiveHour, english)}][{weeklyLabel}:{FormatPercent(weekly, english)}]";
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

    private static void AddProviderReset(
        ICollection<string> lines,
        string provider,
        DateTimeOffset? fiveHourResetAt,
        DateTimeOffset? weeklyResetAt,
        DateTimeOffset now,
        bool english)
    {
        if (!fiveHourResetAt.HasValue && !weeklyResetAt.HasValue)
        {
            return;
        }

        var prefix = provider == "Codex" ? "      Codex" : provider;
        var separator = provider == "Codex" ? "   " : "  ";
        var parts = new List<string>();
        if (fiveHourResetAt.HasValue)
        {
            parts.Add($"{prefix} 5H : {FormatResetTime(fiveHourResetAt.Value, now, english, compactChinese: true)}");
        }

        if (weeklyResetAt.HasValue)
        {
            var compactChinese = provider != "Codex";
            parts.Add($"Week:{FormatResetTime(weeklyResetAt.Value, now, english, compactChinese)}");
        }

        lines.Add(string.Join(separator, parts));
    }

    private static string FormatResetTime(
        DateTimeOffset resetAt,
        DateTimeOffset now,
        bool english,
        bool compactChinese)
    {
        var hours = Math.Max(0, Math.Floor((resetAt - now).TotalHours))
            .ToString("0", CultureInfo.InvariantCulture);
        var remaining = english
            ? $"{hours}h left"
            : compactChinese ? $"余{hours}h" : $"剩余 {hours}h";
        return $"{resetAt.ToLocalTime():yyyy-MM-dd HH:mm} [{remaining}]";
    }

    private static DateTimeOffset LatestRefreshAt(
        WidgetViewState? antigravity,
        CodexWidgetViewState? codex)
    {
        if (antigravity is null)
        {
            return codex?.RefreshedAt ?? DateTimeOffset.Now;
        }

        if (codex is null)
        {
            return antigravity.RefreshedAt;
        }

        return antigravity.RefreshedAt >= codex.RefreshedAt
            ? antigravity.RefreshedAt
            : codex.RefreshedAt;
    }
}
