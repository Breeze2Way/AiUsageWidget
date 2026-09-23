using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

public sealed record AiUsageTooltipLine(string Text, bool IsHighlighted);

public static class AiUsageTooltipPresentation
{
    public static IReadOnlyList<AiUsageTooltipLine> Build(
        string details,
        string? resetDetails,
        UsageProvider activeProvider)
    {
        var lines = new List<AiUsageTooltipLine>();
        UsageProvider? currentProvider = null;
        foreach (var line in SplitLines(details))
        {
            if (line.StartsWith("Antigravity :", StringComparison.Ordinal))
            {
                currentProvider = UsageProvider.Antigravity;
            }
            else if (line.StartsWith("Codex :", StringComparison.Ordinal))
            {
                currentProvider = UsageProvider.Codex;
            }
            else if (line.Length == 0)
            {
                currentProvider = null;
            }

            lines.Add(new AiUsageTooltipLine(line, currentProvider == activeProvider));
        }

        if (resetDetails is null)
        {
            return lines;
        }

        lines.Add(new AiUsageTooltipLine(string.Empty, false));
        foreach (var line in SplitLines(resetDetails))
        {
            UsageProvider? provider = line.StartsWith("Antigravity ", StringComparison.Ordinal)
                ? UsageProvider.Antigravity
                : line.StartsWith("Codex ", StringComparison.Ordinal)
                    ? UsageProvider.Codex
                    : null;
            lines.Add(new AiUsageTooltipLine(line, provider == activeProvider));
        }

        return lines;
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }
}
