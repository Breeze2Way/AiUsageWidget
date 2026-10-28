using System.Globalization;
using AiUsageWidget.Models;

namespace AiUsageWidget.Scheduling;

public static class DailyConversationOptions
{
    public static bool TryParseTime(string? text, out TimeOnly time) => TimeOnly.TryParseExact(
        text?.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static bool IsValid(WidgetSettings settings) => TryParseTime(settings.DailyConversationTime, out _) &&
        (!settings.DailyConversationEnabled || settings.DailyConversationCodex || settings.DailyConversationAntigravity);
}
