using AiUsageWidget.Data;

namespace AiUsageWidget.Services;

public static class AntigravityTokenUsageAggregator
{
    public static AntigravityTokenUsageSummary Aggregate(
        IEnumerable<AntigravityTokenUsageRecord> records,
        DateTimeOffset now)
    {
        var today = now.ToLocalTime().Date;
        var yesterday = today.AddDays(-1);
        var sevenDayStart = today.AddDays(-6);
        var thirtyDayStart = today.AddDays(-29);
        long todayTokens = 0;
        long yesterdayTokens = 0;
        long sevenDayTokens = 0;
        long thirtyDayTokens = 0;

        foreach (var record in records)
        {
            var localDate = record.Timestamp.ToLocalTime().Date;
            if (localDate == today)
            {
                todayTokens += record.TotalTokens;
            }
            else if (localDate == yesterday)
            {
                yesterdayTokens += record.TotalTokens;
            }

            if (localDate >= sevenDayStart && localDate <= today)
            {
                sevenDayTokens += record.TotalTokens;
            }

            if (localDate >= thirtyDayStart && localDate <= today)
            {
                thirtyDayTokens += record.TotalTokens;
            }
        }

        return new AntigravityTokenUsageSummary(
            todayTokens,
            yesterdayTokens,
            sevenDayTokens,
            thirtyDayTokens);
    }
}
