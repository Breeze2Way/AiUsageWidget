namespace AiUsageWidget.Tests;

public sealed class AiUsageTooltipPresentationTests
{
    private static readonly string Details = string.Join(Environment.NewLine,
        "Antigravity : 7.1M[昨日:125.6M，7天:169.4M，30天1175.5M]",
        "     Gemini : [5h:92.8%][周:33.6%]",
        "     Claude : [5h:100%][周:19.1%]",
        string.Empty,
        "Codex : 29.1M[昨日:73.7M，7天:169.4M，30天1175.5M]",
        "      Codex : [5h:95%][周:23%]");

    private static readonly string Resets = string.Join(Environment.NewLine,
        "重置时间[2026-09-23 13:59:25更新]:",
        "Antigravity 5H:09-23 15:21:00 [余1h]  Week : 09-24 10:15:00 [余20h]",
        "Codex       5H:09-23 15:54:00 [余1h]  Week : 09-27 09:29:00 [余91h]");

    [Fact]
    public void HighlightsTheAntigravityBlockWhenItsQuotaIsInTheCircle()
    {
        var lines = AiUsageTooltipPresentation.Build(
            Details,
            Resets,
            UsageProvider.Antigravity);

        AssertHighlighted(lines, "Antigravity :", expected: true);
        AssertHighlighted(lines, "Gemini :", expected: true);
        AssertHighlighted(lines, "Claude :", expected: true);
        AssertHighlighted(lines, "Antigravity 5H:", expected: true);
        AssertHighlighted(lines, "Codex : 29.1M", expected: false);
        AssertHighlighted(lines, "Codex : [5h:", expected: false);
        AssertHighlighted(lines, "Codex       5H:", expected: false);
        AssertHighlighted(lines, "重置时间[", expected: false);
    }

    [Fact]
    public void HighlightsTheCodexBlockWhenItsQuotaIsInTheCircle()
    {
        var lines = AiUsageTooltipPresentation.Build(
            Details,
            Resets,
            UsageProvider.Codex);

        AssertHighlighted(lines, "Antigravity :", expected: false);
        AssertHighlighted(lines, "Gemini :", expected: false);
        AssertHighlighted(lines, "Claude :", expected: false);
        AssertHighlighted(lines, "Antigravity 5H:", expected: false);
        AssertHighlighted(lines, "Codex : 29.1M", expected: true);
        AssertHighlighted(lines, "Codex : [5h:", expected: true);
        AssertHighlighted(lines, "Codex       5H:", expected: true);
        AssertHighlighted(lines, "重置时间[", expected: false);
    }

    [Fact]
    public void KeepsIndentedAntigravityGroupsInTheAntigravityBlock()
    {
        var details = string.Join(Environment.NewLine,
            "Antigravity : usage",
            "     Codex : [5h:80%][周:70%]",
            string.Empty,
            "Codex : usage");

        var lines = AiUsageTooltipPresentation.Build(
            details,
            resetDetails: null,
            UsageProvider.Antigravity);

        Assert.True(Assert.Single(
            lines,
            line => line.Text == "     Codex : [5h:80%][周:70%]").IsHighlighted);
        Assert.False(Assert.Single(
            lines,
            line => line.Text == "Codex : usage").IsHighlighted);
    }

    private static void AssertHighlighted(
        IReadOnlyList<AiUsageTooltipLine> lines,
        string text,
        bool expected)
    {
        var line = Assert.Single(lines, candidate => candidate.Text.Contains(text));
        Assert.Equal(expected, line.IsHighlighted);
    }
}
