using AiUsageWidget.Data;

namespace AiUsageWidget.Tests;

public sealed class MainWindowCompositionTests
{
    [Fact]
    public void UsesGreenOnlyForTheSelectedQuotaRow()
    {
        System.Windows.Media.Color? antigravityColor = null;
        System.Windows.Media.Color? codexColor = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var inlines = MainWindow.CreateTooltipInlines(
                    string.Join(Environment.NewLine,
                        "Gemini [5h : 80%] [周 : 70%]",
                        "用量 : 1.0M [昨日:2.0M  7天:3.0M  30天4.0M]",
                        "Codex [5h : 60%] [周 : 50%]"),
                    UsageProvider.Antigravity,
                    AntigravityQuotaGroup.Gemini);
                var runs = inlines.OfType<System.Windows.Documents.Run>().ToArray();
                antigravityColor = ((System.Windows.Media.SolidColorBrush)runs[0].Foreground).Color;
                Assert.Equal(
                    System.Windows.Media.Colors.White,
                    ((System.Windows.Media.SolidColorBrush)runs[1].Foreground).Color);
                codexColor = ((System.Windows.Media.SolidColorBrush)runs[2].Foreground).Color;
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
    public void PreservesTheFormattedTooltipLineOrder()
    {
        var lines = AiUsageTooltipPresentation.Build(
            "Codex [5h : 69%] [周 : 13%]" + Environment.NewLine +
            "重置 : 09-23 20:21:08 [余3h]" + Environment.NewLine +
            "2026-09-23 17:12:48",
            UsageProvider.Codex,
            AntigravityQuotaGroup.Unknown);

        Assert.Equal(
            [
                "Codex [5h : 69%] [周 : 13%]",
                "重置 : 09-23 20:21:08 [余3h]",
                "2026-09-23 17:12:48"
            ],
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
