using AiUsageWidget.Data;

namespace AiUsageWidget.Tests;

public sealed class MainWindowCompositionTests
{
    [Fact]
    public void UsesGreenForTheCircleProviderAndWhiteForTheOtherProvider()
    {
        System.Windows.Media.Color? antigravityColor = null;
        System.Windows.Media.Color? codexColor = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var inlines = MainWindow.CreateTooltipInlines(
                    "Antigravity : usage" + Environment.NewLine + "Codex : usage",
                    resetDetails: null,
                    UsageProvider.Antigravity);
                var runs = inlines.OfType<System.Windows.Documents.Run>().ToArray();
                antigravityColor = ((System.Windows.Media.SolidColorBrush)runs[0].Foreground).Color;
                codexColor = ((System.Windows.Media.SolidColorBrush)runs[1].Foreground).Color;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Equal(System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80), antigravityColor);
        Assert.Equal(System.Windows.Media.Colors.White, codexColor);
    }

    [Fact]
    public void SeparatesUsageAndResetSectionsWithABlankLine()
    {
        var lines = AiUsageTooltipPresentation.Build(
            "Codex : usage",
            "重置时间[2026-09-23 13:59:25更新]:",
            UsageProvider.Codex);

        Assert.Equal(
            ["Codex : usage", string.Empty, "重置时间[2026-09-23 13:59:25更新]:"],
            lines.Select(line => line.Text));
    }

    [Fact]
    public void CreatesReadableWideMonospacedTooltipText()
    {
        double? maxWidth = null;
        string? fontFamily = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var textBlock = MainWindow.CreateDetailsTextBlock();
                maxWidth = textBlock.MaxWidth;
                fontFamily = textBlock.FontFamily.Source;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Equal(600, maxWidth);
        Assert.Equal("Consolas", fontFamily);
    }

    [Fact]
    public void CreatesRefreshServiceWithOfficialUsageReader()
    {
        var service = MainWindow.CreateRefreshService(
            () => new AntigravityQuotaSnapshot(
                "Pro",
                [new("Gemini", null, 42, null, AntigravityQuotaPeriod.Weekly)],
                DateTimeOffset.UtcNow),
            _ => new AntigravityTokenUsageSummary(1_200_000, 800_000));

        var state = service.Refresh(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(42, state.OfficialRemainingPercent);
        Assert.Equal(1_200_000, state.TodayTokens);
        Assert.Equal(800_000, state.YesterdayTokens);
    }
}
