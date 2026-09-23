using CodexTokenUsage = CodexUsageWidget.Models.TokenUsage;
using CodexUsageSnapshot = CodexUsageWidget.Models.UsageSnapshot;
using CodexWidgetViewState = CodexUsageWidget.Models.WidgetViewState;

namespace AiUsageWidget.Tests;

public sealed class AiUsageTooltipFormatterTests
{
    [Fact]
    public void FormatsChineseUsageBlocksInTheRequestedCompactLayout()
    {
        var refreshedAt = new DateTimeOffset(2026, 9, 23, 13, 59, 25, TimeSpan.FromHours(8));
        var antigravity = CreateAntigravityState(refreshedAt);
        var codex = CreateCodexState(refreshedAt);

        var details = AiUsageTooltipFormatter.FormatDetails(
            antigravity,
            codex,
            refreshedAt,
            english: false);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Antigravity : 7.1M[昨日:125.6M，7天:169.4M，30天1175.5M]",
                "     Gemini : [5h:92.8%][周:33.6%]",
                "     Claude : [5h:100%][周:19.1%]",
                string.Empty,
                "Codex : 29.1M[昨日:73.7M，7天:169.4M，30天1175.5M]",
                "      Codex : [5h:95%][周:23%]"),
            details);
    }

    [Fact]
    public void FormatsChineseResetTimesWithTheUpdateTimeInTheHeading()
    {
        var refreshedAt = new DateTimeOffset(2026, 9, 23, 13, 59, 25, TimeSpan.FromHours(8));
        var now = new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.FromHours(8));
        var antigravity = CreateAntigravityState(refreshedAt);
        var codex = CreateCodexState(refreshedAt);

        var details = AiUsageTooltipFormatter.FormatResetDetails(
            antigravity,
            codex,
            now,
            english: false);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "重置时间[2026-09-23 13:59:25更新]:",
                "Antigravity 5H:09-23 15:21:00 [余1h]  Week : 09-24 10:15:00 [余20h]",
                "Codex       5H:09-23 15:54:00 [余1h]  Week : 09-27 09:29:00 [余91h]"),
            details);
    }

    [Fact]
    public void FormatsEnglishUsageAndResetLabelsWithoutChineseText()
    {
        var refreshedAt = new DateTimeOffset(2026, 9, 23, 13, 59, 25, TimeSpan.FromHours(8));
        var antigravity = CreateAntigravityState(refreshedAt);
        var codex = CreateCodexState(refreshedAt);

        var details = AiUsageTooltipFormatter.FormatDetails(
            antigravity,
            codex,
            refreshedAt,
            english: true);
        var resets = AiUsageTooltipFormatter.FormatResetDetails(
            antigravity,
            codex,
            new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.FromHours(8)),
            english: true);

        Assert.Contains("[Yesterday:125.6M, 7 days:169.4M, 30 days:1175.5M]", details);
        Assert.Contains("[5h:92.8%][Week:33.6%]", details);
        Assert.DoesNotContain("昨日", details);
        Assert.Contains("Reset times [updated 2026-09-23 13:59:25]:", resets);
        Assert.Contains("[1h left]", resets);
        Assert.DoesNotContain("重置", resets);
    }

    [Fact]
    public void KeepsProviderNamesWhenOnlyWeeklyResetTimesAreAvailable()
    {
        var refreshedAt = new DateTimeOffset(2026, 9, 23, 13, 59, 25, TimeSpan.FromHours(8));
        var antigravity = CreateAntigravityState(refreshedAt) with { FiveHourResetAt = null };
        var codex = CreateCodexState(refreshedAt) with { FiveHourResetAt = null };

        var resets = AiUsageTooltipFormatter.FormatResetDetails(
            antigravity,
            codex,
            new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.FromHours(8)),
            english: false);

        Assert.Contains(
            "Antigravity Week : 09-24 10:15:00 [余20h]",
            resets);
        Assert.Contains(
            "Codex       Week : 09-27 09:29:00 [余91h]",
            resets);
    }

    [Fact]
    public void KeepsTheUpdateTimeWhenResetTimesAreUnavailable()
    {
        var refreshedAt = new DateTimeOffset(2026, 9, 23, 13, 59, 25, TimeSpan.FromHours(8));
        var antigravity = CreateAntigravityState(refreshedAt) with
        {
            FiveHourResetAt = null,
            WeeklyResetAt = null,
            ResetAt = null
        };
        var codex = CreateCodexState(refreshedAt) with
        {
            FiveHourResetAt = null,
            WeeklyResetAt = null,
            ResetAt = null
        };

        var resets = AiUsageTooltipFormatter.FormatResetDetails(
            antigravity,
            codex,
            refreshedAt,
            english: false);

        Assert.Equal("重置时间[2026-09-23 13:59:25更新]:", resets);
    }

    private static WidgetViewState CreateAntigravityState(DateTimeOffset refreshedAt)
    {
        return new WidgetViewState(refreshedAt, "ok", false, 33.6)
        {
            TodayTokens = 7_100_000,
            YesterdayTokens = 125_600_000,
            SevenDayTokens = 169_400_000,
            ThirtyDayTokens = 1_175_500_000,
            FiveHourRemainingPercent = 92.8,
            FiveHourResetAt = new DateTimeOffset(2026, 9, 23, 15, 21, 0, TimeSpan.FromHours(8)),
            WeeklyResetAt = new DateTimeOffset(2026, 9, 24, 10, 15, 0, TimeSpan.FromHours(8)),
            Quota = new AntigravityDisplayQuota(
                "Pro",
                92.8,
                null,
                33.6,
                null,
                [
                    new("Gemini 5h", "Gemini Models", 92.8, null, AntigravityQuotaPeriod.Short),
                    new("Gemini week", "Gemini Models", 33.6, null, AntigravityQuotaPeriod.Weekly),
                    new("Claude 5h", "Claude and GPT models", 100, null, AntigravityQuotaPeriod.Short),
                    new("Claude week", "Claude and GPT models", 19.1, null, AntigravityQuotaPeriod.Weekly)
                ])
        };
    }

    private static CodexWidgetViewState CreateCodexState(DateTimeOffset refreshedAt)
    {
        return new CodexWidgetViewState(
            Snapshot(0),
            Snapshot(169_400_000),
            Snapshot(1_175_500_000),
            refreshedAt,
            "ok",
            false,
            23)
        {
            TodayTokens = 29_100_000,
            YesterdayTokens = 73_700_000,
            FiveHourRemainingPercent = 95,
            FiveHourResetAt = new DateTimeOffset(2026, 9, 23, 15, 54, 0, TimeSpan.FromHours(8)),
            WeeklyResetAt = new DateTimeOffset(2026, 9, 27, 9, 29, 0, TimeSpan.FromHours(8))
        };
    }

    private static CodexUsageSnapshot Snapshot(long totalTokens)
    {
        return new CodexUsageSnapshot(
            new CodexTokenUsage(0, 0, 0, 0, 0, totalTokens),
            0,
            true,
            0,
            100);
    }
}
