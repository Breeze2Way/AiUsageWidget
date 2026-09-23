using AiUsageWidget.Models;
using CodexUsageWidget.Data;
using CodexUsageWidget.Models;
using CodexUsageWidget.Services;

using AiWidgetSettings = AiUsageWidget.Models.WidgetSettings;
using CodexUsageRefreshService = CodexUsageWidget.Services.UsageRefreshService;
using CodexWidgetSettings = CodexUsageWidget.Models.WidgetSettings;
using CodexWidgetViewState = CodexUsageWidget.Models.WidgetViewState;

namespace AiUsageWidget.Services;

public sealed class CodexUsageProvider
{
    private readonly CodexUsageRefreshService refreshService;

    public CodexUsageProvider()
    {
        var apiReader = new OfficialUsageApiReader();
        refreshService = new CodexUsageRefreshService(
            new CodexDataReader(),
            CodexDataPaths.ForCurrentUser(),
            readOfficialUsage: apiReader.ReadUsage);
    }

    public CodexWidgetViewState Refresh(
        DateTimeOffset now,
        AiWidgetSettings settings,
        bool refreshOfficial = true)
    {
        var codexSettings = new CodexWidgetSettings(
            WeeklyBudgetTokens: settings.WeeklyBudgetTokens,
            RefreshSeconds: settings.RefreshSeconds,
            Opacity: settings.Opacity,
            Topmost: settings.Topmost,
            AutoStart: settings.AutoStart,
            Left: settings.Left,
            Top: settings.Top)
        {
            WeeklyBudgetConfigured = settings.WeeklyBudgetConfigured,
            Language = settings.Language,
            WeeklyRingColor = settings.WeeklyRingColor,
            WeeklyRingGradientColor = settings.WeeklyRingGradientColor,
            WeeklyRingGradientEnabled = settings.WeeklyRingGradientEnabled,
            WeeklyRingTrackColor = settings.WeeklyRingTrackColor
        };

        return refreshService.Refresh(now, codexSettings, refreshOfficial);
    }
}
