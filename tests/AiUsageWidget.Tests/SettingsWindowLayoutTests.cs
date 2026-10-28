namespace AiUsageWidget.Tests;

public sealed class SettingsWindowLayoutTests
{
    [Fact]
    public void SettingsWindowLeavesRoomForColorRowsAndActionButtons()
    {
        Assert.True(MainWindow.SettingsWindowWidth >= 480);
        Assert.True(MainWindow.SettingsWindowHeight >= 540);
    }

    [Fact]
    public void SettingsWindowAndFooterFitShortWorkArea()
    {
        var area = new System.Windows.Rect(0, 0, 1024, 512);
        var size = MainWindow.CalculateSettingsSize(area);
        Assert.InRange(size.Height, 300, 492);
        Assert.InRange(size.Width, 300, 1004);
    }
}
