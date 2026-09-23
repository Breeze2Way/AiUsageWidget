using System.Globalization;
using AiUsageWidget.Data;
using AiUsageWidget.Models;
using CodexWidgetViewState = CodexUsageWidget.Models.WidgetViewState;

namespace AiUsageWidget.Services;

public static class AiUsageTooltipFormatter
{
    public static string FormatDetails(
        WidgetViewState? antigravity,
        CodexWidgetViewState? codex,
        DateTimeOffset now,
        bool english)
    {
        var lines = new List<string>();
        if (antigravity is not null)
        {
            AddAntigravityQuotaLines(lines, antigravity, english);
            lines.Add(FormatTokenUsage(
                antigravity.TodayTokens,
                antigravity.YesterdayTokens,
                antigravity.SevenDayTokens,
                antigravity.ThirtyDayTokens,
                english,
                commaSeparated: false));
            AddResetLine(
                lines,
                antigravity.FiveHourResetAt,
                antigravity.WeeklyResetAt ?? antigravity.ResetAt,
                now,
                english);
        }

        if (codex is not null)
        {
            AddBlockSeparator(lines);
            lines.Add(FormatQuotaLine(
                "Codex",
                codex.FiveHourRemainingPercent,
                codex.OfficialRemainingPercent,
                english));
            lines.Add(FormatTokenUsage(
                codex.TodayTokens,
                codex.YesterdayTokens,
                codex.SevenDay.Usage.TotalTokens,
                codex.ThirtyDay.Usage.TotalTokens,
                english,
                commaSeparated: true));
            AddResetLine(
                lines,
                codex.FiveHourResetAt,
                codex.WeeklyResetAt ?? codex.ResetAt,
                now,
                english);
        }

        if (antigravity is not null || codex is not null)
        {
            AddBlockSeparator(lines);
            lines.Add(LatestRefreshAt(antigravity, codex).ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        return string.Join(Environment.NewLine, lines);
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
                "Antigravity",
                state.FiveHourRemainingPercent,
                state.OfficialRemainingPercent,
                english));
            return;
        }

        var recognizedGroups = quota.Rows
            .GroupBy(row => row.Group ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Rows: group, Name: FormatGroupName(group.Key)))
            .Where(group => group.Name is not null)
            .ToArray();
        if (recognizedGroups.Length == 0)
        {
            lines.Add(FormatQuotaLine(
                english ? "Models" : "模型",
                quota.ShortRemainingPercent,
                quota.WeeklyRemainingPercent,
                english));
            return;
        }

        foreach (var group in recognizedGroups)
        {
            var shortPercent = FindLowestPercent(group.Rows, AntigravityQuotaPeriod.Short);
            var weeklyPercent = FindLowestPercent(group.Rows, AntigravityQuotaPeriod.Weekly);
            lines.Add(FormatQuotaLine(group.Name!, shortPercent, weeklyPercent, english));
        }
    }

    private static double? FindLowestPercent(
        IEnumerable<AntigravityQuotaRow> rows,
        AntigravityQuotaPeriod period)
    {
        return rows
            .Where(row => row.Period == period)
            .Select(row => (double?)row.RemainingPercent)
            .OrderBy(value => value)
            .FirstOrDefault();
    }

    private static string? FormatGroupName(string group)
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

        return null;
    }

    private static string FormatTokenUsage(
        long today,
        long yesterday,
        long sevenDay,
        long thirtyDay,
        bool english,
        bool commaSeparated)
    {
        var label = english ? "Usage" : "用量";
        var yesterdayLabel = english ? "Yesterday" : "昨日";
        var sevenDayLabel = english ? "7 days" : "7天";
        var thirtyDayLabel = english ? "30 days" : "30天";
        var separator = commaSeparated ? ", " : "  ";
        return $"{label} : {FormatMillions(today)} " +
               $"[{yesterdayLabel}:{FormatMillions(yesterday)}{separator}" +
               $"{sevenDayLabel}:{FormatMillions(sevenDay)}{separator}" +
               $"{thirtyDayLabel}{(english ? ":" : string.Empty)}{FormatMillions(thirtyDay)}]";
    }

    private static string FormatQuotaLine(
        string name,
        double? fiveHour,
        double? weekly,
        bool english)
    {
        var weeklyLabel = english ? "Week" : "周";
        return $"{name} [5h : {FormatPercent(fiveHour, english)}] " +
               $"[{weeklyLabel} : {FormatPercent(weekly, english)}]";
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

    private static void AddResetLine(
        ICollection<string> lines,
        DateTimeOffset? fiveHourResetAt,
        DateTimeOffset? weeklyResetAt,
        DateTimeOffset now,
        bool english)
    {
        if (!fiveHourResetAt.HasValue && !weeklyResetAt.HasValue)
        {
            return;
        }

        var parts = new List<string>();
        if (fiveHourResetAt.HasValue)
        {
            parts.Add(FormatResetTime(fiveHourResetAt.Value, now, english));
        }

        if (weeklyResetAt.HasValue)
        {
            parts.Add($"Week :{FormatResetTime(weeklyResetAt.Value, now, english)}");
        }

        lines.Add($"{(english ? "Reset" : "重置")} : {string.Join("   ", parts)}");
    }

    private static string FormatResetTime(
        DateTimeOffset resetAt,
        DateTimeOffset now,
        bool english)
    {
        var hours = Math.Max(0, Math.Floor((resetAt - now).TotalHours))
            .ToString("0", CultureInfo.InvariantCulture);
        var remaining = english ? $"{hours}h left" : $"余{hours}h";
        return $"{resetAt.ToLocalTime():MM-dd HH:mm:ss} [{remaining}]";
    }

    private static void AddBlockSeparator(ICollection<string> lines)
    {
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }
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
