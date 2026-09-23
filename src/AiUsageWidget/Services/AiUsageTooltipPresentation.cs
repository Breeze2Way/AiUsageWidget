using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

public sealed record AiUsageTooltipLine(string Text, bool IsHighlighted);

public static class AiUsageTooltipPresentation
{
    public static IReadOnlyList<AiUsageTooltipLine> Build(
        string details,
        UsageProvider activeProvider,
        AntigravityQuotaGroup selectedAntigravityGroup)
    {
        return SplitLines(details)
            .Select(line => new AiUsageTooltipLine(
                line,
                IsSelectedQuotaLine(line, activeProvider, selectedAntigravityGroup)))
            .ToArray();
    }

    private static bool IsSelectedQuotaLine(
        string line,
        UsageProvider activeProvider,
        AntigravityQuotaGroup selectedAntigravityGroup)
    {
        if (activeProvider == UsageProvider.Codex)
        {
            return line.StartsWith("Codex [", StringComparison.Ordinal);
        }

        return selectedAntigravityGroup switch
        {
            AntigravityQuotaGroup.Gemini => line.StartsWith("Gemini [", StringComparison.Ordinal),
            AntigravityQuotaGroup.Claude => line.StartsWith("Claude [", StringComparison.Ordinal),
            _ => false
        };
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }
}
