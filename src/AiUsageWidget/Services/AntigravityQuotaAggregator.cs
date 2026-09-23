using AiUsageWidget.Data;

namespace AiUsageWidget.Services;

public enum AntigravityQuotaGroup
{
    Unknown,
    Gemini,
    Claude
}

public sealed record AntigravityDisplayQuota(
    string? PlanName,
    double? ShortRemainingPercent,
    DateTimeOffset? ShortResetAt,
    double? WeeklyRemainingPercent,
    DateTimeOffset? WeeklyResetAt,
    IReadOnlyList<AntigravityQuotaRow> Rows)
{
    public AntigravityQuotaGroup SelectedGroup { get; init; }
}

public static class AntigravityQuotaAggregator
{
    public static AntigravityDisplayQuota Aggregate(AntigravityQuotaSnapshot snapshot)
    {
        var selection = FindRowsForSelectedModel(snapshot);
        var rowsForSelectedModel = selection.Rows;
        var shortQuota = FindLowest(rowsForSelectedModel, AntigravityQuotaPeriod.Short);
        var weeklyQuota = FindLowest(rowsForSelectedModel, AntigravityQuotaPeriod.Weekly);
        return new AntigravityDisplayQuota(
            snapshot.PlanName,
            shortQuota?.RemainingPercent,
            shortQuota?.ResetAt,
            weeklyQuota?.RemainingPercent,
            weeklyQuota?.ResetAt,
            snapshot.Rows)
        {
            SelectedGroup = selection.Group
        };
    }

    private static (IReadOnlyList<AntigravityQuotaRow> Rows, AntigravityQuotaGroup Group)
        FindRowsForSelectedModel(
        AntigravityQuotaSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.SelectedModelId))
        {
            var exactRows = snapshot.Rows
                .Where(row => string.Equals(row.ModelId, snapshot.SelectedModelId, StringComparison.Ordinal))
                .ToArray();
            if (exactRows.Length > 0)
            {
                return (exactRows, ParseGroup(exactRows[0].Group));
            }
        }

        var selectedGroupName = GetSelectedGroupName(snapshot.SelectedModelLabel);
        if (selectedGroupName is not null)
        {
            var groupRows = snapshot.Rows
                .Where(row => string.Equals(row.Group, selectedGroupName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (groupRows.Length > 0)
            {
                return (groupRows, ParseGroup(selectedGroupName));
            }
        }

        return (snapshot.Rows, AntigravityQuotaGroup.Unknown);
    }

    private static string? GetSelectedGroupName(string? selectedModelLabel)
    {
        if (string.IsNullOrWhiteSpace(selectedModelLabel))
        {
            return null;
        }

        if (selectedModelLabel.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemini Models";
        }

        if (selectedModelLabel.Contains("Claude", StringComparison.OrdinalIgnoreCase) ||
            selectedModelLabel.Contains("GPT", StringComparison.OrdinalIgnoreCase))
        {
            return "Claude and GPT models";
        }

        return null;
    }

    private static AntigravityQuotaGroup ParseGroup(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return AntigravityQuotaGroup.Unknown;
        }

        if (group.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return AntigravityQuotaGroup.Gemini;
        }

        if (group.Contains("Claude", StringComparison.OrdinalIgnoreCase) ||
            group.Contains("GPT", StringComparison.OrdinalIgnoreCase))
        {
            return AntigravityQuotaGroup.Claude;
        }

        return AntigravityQuotaGroup.Unknown;
    }

    private static AntigravityQuotaRow? FindLowest(
        IReadOnlyList<AntigravityQuotaRow> rows,
        AntigravityQuotaPeriod period)
    {
        return rows
            .Where(row => row.Period == period)
            .OrderBy(row => row.RemainingPercent)
            .FirstOrDefault();
    }
}
