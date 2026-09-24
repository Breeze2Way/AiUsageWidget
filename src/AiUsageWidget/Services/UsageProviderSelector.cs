using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

public static class UsageProviderSelector
{
    public static UsageProvider Select(string? foregroundProcessName, UsageProvider? previousProvider = null)
    {
        return GetProvider(foregroundProcessName) ?? previousProvider ?? UsageProvider.Antigravity;
    }

    public static UsageProvider Select(
        string? foregroundProcessName,
        UsageProvider? previousProvider,
        IReadOnlySet<UsageProvider> availableProviders)
    {
        var foregroundProvider = GetProvider(foregroundProcessName);
        if (foregroundProvider.HasValue && availableProviders.Contains(foregroundProvider.Value))
        {
            return foregroundProvider.Value;
        }

        if (previousProvider.HasValue && availableProviders.Contains(previousProvider.Value))
        {
            return previousProvider.Value;
        }

        if (availableProviders.Contains(UsageProvider.Antigravity))
        {
            return UsageProvider.Antigravity;
        }

        if (availableProviders.Contains(UsageProvider.Codex))
        {
            return UsageProvider.Codex;
        }

        return previousProvider ?? UsageProvider.Antigravity;
    }

    private static UsageProvider? GetProvider(string? processName)
    {
        if (string.Equals(processName, "ChatGPT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(processName, "Codex", StringComparison.OrdinalIgnoreCase))
        {
            return UsageProvider.Codex;
        }

        return string.Equals(processName, "Antigravity", StringComparison.OrdinalIgnoreCase)
            ? UsageProvider.Antigravity
            : null;
    }
}
