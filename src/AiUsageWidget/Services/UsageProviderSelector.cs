using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

public static class UsageProviderSelector
{
    public static UsageProvider Select(string? foregroundProcessName, UsageProvider? previousProvider = null)
    {
        if (string.Equals(foregroundProcessName, "ChatGPT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(foregroundProcessName, "Codex", StringComparison.OrdinalIgnoreCase))
        {
            return UsageProvider.Codex;
        }

        if (string.Equals(foregroundProcessName, "Antigravity", StringComparison.OrdinalIgnoreCase))
        {
            return UsageProvider.Antigravity;
        }

        return previousProvider ?? UsageProvider.Antigravity;
    }
}
