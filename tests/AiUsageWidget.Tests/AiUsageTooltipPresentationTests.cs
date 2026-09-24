namespace AiUsageWidget.Tests;

public sealed class AiUsageTooltipPresentationTests
{
    private static readonly string Details = string.Join(Environment.NewLine,
        "Gemini [5h : 92.8%] [周 : 33.6%]",
        "Claude [5h : 100%] [周 : 19.1%]",
        "用量 : 7.1M [昨日:125.6M  7天:169.4M  30天1175.5M]",
        "重置 : 09-23 15:21:00 [余1h]   Week :09-24 10:15:00 [余20h]",
        string.Empty,
        "Codex [5h : 95%] [周 : 23%]",
        "用量 : 29.1M [昨日:73.7M, 7天:169.4M, 30天1175.5M]",
        "重置 : 09-23 15:54:00 [余1h]   Week :09-27 09:29:00 [余91h]",
        string.Empty,
        "2026-09-23 13:59:25");

    [Fact]
    public void HighlightsOnlyTheSelectedGeminiQuotaRowForAntigravity()
    {
        var lines = AiUsageTooltipPresentation.Build(
            Details,
            UsageProvider.Antigravity,
            AntigravityQuotaGroup.Gemini);

        AssertHighlighted(lines, "Gemini [", expected: true);
        AssertHighlighted(lines, "Claude [", expected: false);
        AssertHighlighted(lines, "Codex [", expected: false);
        AssertAllNonQuotaRowsAreWhite(lines);
    }

    [Fact]
    public void HighlightsOnlyTheSelectedClaudeQuotaRowForAntigravity()
    {
        var lines = AiUsageTooltipPresentation.Build(
            Details,
            UsageProvider.Antigravity,
            AntigravityQuotaGroup.Claude);

        AssertHighlighted(lines, "Gemini [", expected: false);
        AssertHighlighted(lines, "Claude [", expected: true);
        AssertHighlighted(lines, "Codex [", expected: false);
        AssertAllNonQuotaRowsAreWhite(lines);
    }

    [Fact]
    public void HighlightsOnlyTheCodexQuotaRowForCodex()
    {
        var lines = AiUsageTooltipPresentation.Build(
            Details,
            UsageProvider.Codex,
            AntigravityQuotaGroup.Gemini);

        AssertHighlighted(lines, "Gemini [", expected: false);
        AssertHighlighted(lines, "Claude [", expected: false);
        AssertHighlighted(lines, "Codex [", expected: true);
        AssertAllNonQuotaRowsAreWhite(lines);
    }

    [Fact]
    public void HighlightsSingleProviderCodexQuotaRowForCodex()
    {
        var details = "Codex : [5h:99%] [周:4%]";

        var lines = AiUsageTooltipPresentation.Build(
            details,
            UsageProvider.Codex,
            AntigravityQuotaGroup.Unknown);

        Assert.True(Assert.Single(lines).IsHighlighted);
    }

    [Fact]
    public void HighlightsSingleProviderAntigravityQuotaRowForAntigravity()
    {
        var details = "Antigravity : [5h:99%] [周:4%]";

        var lines = AiUsageTooltipPresentation.Build(
            details,
            UsageProvider.Antigravity,
            AntigravityQuotaGroup.Unknown);

        Assert.True(Assert.Single(lines).IsHighlighted);
    }

    [Fact]
    public void UnknownAntigravitySelectionHighlightsNoQuotaRow()
    {
        var lines = AiUsageTooltipPresentation.Build(
            Details,
            UsageProvider.Antigravity,
            AntigravityQuotaGroup.Unknown);

        Assert.DoesNotContain(lines, line => line.IsHighlighted);
    }

    [Fact]
    public void CodexProviderDoesNotHighlightAnAntigravityGroupNamedCodex()
    {
        var details = string.Join(Environment.NewLine,
            "Antig Codex [5h : 20%] [周 : 10%]",
            string.Empty,
            "Codex [5h : 95%] [周 : 23%]");

        var lines = AiUsageTooltipPresentation.Build(
            details,
            UsageProvider.Codex,
            AntigravityQuotaGroup.Unknown);

        Assert.False(Assert.Single(lines, line => line.Text.StartsWith("Antig Codex", StringComparison.Ordinal)).IsHighlighted);
        Assert.True(Assert.Single(lines, line => line.Text.StartsWith("Codex [", StringComparison.Ordinal)).IsHighlighted);
    }

    private static void AssertAllNonQuotaRowsAreWhite(IReadOnlyList<AiUsageTooltipLine> lines)
    {
        Assert.All(
            lines.Where(line => line.Text.StartsWith("用量 ", StringComparison.Ordinal) ||
                                line.Text.StartsWith("重置 ", StringComparison.Ordinal) ||
                                line.Text.StartsWith("2026-", StringComparison.Ordinal) ||
                                line.Text.Length == 0),
            line => Assert.False(line.IsHighlighted));
    }

    private static void AssertHighlighted(
        IReadOnlyList<AiUsageTooltipLine> lines,
        string text,
        bool expected)
    {
        var line = Assert.Single(lines, candidate => candidate.Text.StartsWith(text, StringComparison.Ordinal));
        Assert.Equal(expected, line.IsHighlighted);
    }
}
