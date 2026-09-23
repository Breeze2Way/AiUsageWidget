using System.Windows;
using AiUsageWidget.Services;

namespace AiUsageWidget.Tests;

public sealed class WindowPlacementCalculatorTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);
    private static readonly Size SettingsSize = new(360, 245);

    [Fact]
    public void RestoresMinimizedWindowToNormalState()
    {
        Assert.Equal(WindowState.Normal, WindowRestorePolicy.GetRestoredState(WindowState.Minimized));
    }

    [Fact]
    public void PlacesSettingsToLeftWhenBallIsAtRightEdge()
    {
        var ball = new Rect(1852, 24, 68, 68);

        var position = WindowPlacementCalculator.CalculateSettingsPosition(
            ball,
            SettingsSize,
            WorkArea);

        Assert.Equal(1480, position.X);
        Assert.InRange(position.Y, WorkArea.Top, WorkArea.Bottom - SettingsSize.Height);
    }

    [Fact]
    public void PlacesSettingsToRightWhenBallIsAtLeftEdge()
    {
        var ball = new Rect(0, 450, 68, 68);

        var position = WindowPlacementCalculator.CalculateSettingsPosition(
            ball,
            SettingsSize,
            WorkArea);

        Assert.Equal(80, position.X);
        Assert.InRange(position.Y, WorkArea.Top, WorkArea.Bottom - SettingsSize.Height);
    }

    [Fact]
    public void ClampsSettingsInsideWorkAreaWhenNoSideFits()
    {
        var smallWorkArea = new Rect(0, 0, 300, 180);
        var ball = new Rect(120, 70, 68, 68);

        var position = WindowPlacementCalculator.CalculateSettingsPosition(
            ball,
            SettingsSize,
            smallWorkArea);

        Assert.Equal(smallWorkArea.Left, position.X);
        Assert.Equal(smallWorkArea.Top, position.Y);
    }

    [Fact]
    public void ClampsSavedBallPositionInsideWorkArea()
    {
        var position = WindowPlacementCalculator.ClampInsideWorkArea(
            new Point(-694, 54),
            new Size(68, 68),
            WorkArea);

        Assert.Equal(WorkArea.Left, position.X);
        Assert.Equal(54, position.Y);
    }

    [Fact]
    public void FindsMatchingWorkAreaForNegativeCoordinateSecondaryScreen()
    {
        var secondaryWorkArea = new Rect(-1920, 0, 1920, 1040);
        var primaryWorkArea = new Rect(0, 0, 1920, 1040);
        var workAreas = new[] { secondaryWorkArea, primaryWorkArea };

        var best = WindowPlacementCalculator.FindBestWorkArea(
            new Point(-68, 400),
            workAreas,
            primaryWorkArea);

        Assert.Equal(secondaryWorkArea, best);
    }

    [Fact]
    public void FindsMatchingWorkAreaForPrimaryScreen()
    {
        var secondaryWorkArea = new Rect(-1920, 0, 1920, 1040);
        var primaryWorkArea = new Rect(0, 0, 1920, 1040);
        var workAreas = new[] { secondaryWorkArea, primaryWorkArea };

        var best = WindowPlacementCalculator.FindBestWorkArea(
            new Point(500, 300),
            workAreas,
            secondaryWorkArea);

        Assert.Equal(primaryWorkArea, best);
    }

    [Fact]
    public void FindsClosestWorkAreaWhenPositionIsOutsideAllScreens()
    {
        var secondaryWorkArea = new Rect(-1920, 0, 1920, 1040);
        var primaryWorkArea = new Rect(0, 0, 1920, 1040);
        var workAreas = new[] { secondaryWorkArea, primaryWorkArea };

        // Point far to the left of secondary screen
        var best = WindowPlacementCalculator.FindBestWorkArea(
            new Point(-2500, 500),
            workAreas,
            primaryWorkArea);

        Assert.Equal(secondaryWorkArea, best);
    }

    [Fact]
    public void ReturnsFallbackWhenWorkAreasListIsEmpty()
    {
        var fallback = new Rect(0, 0, 800, 600);
        var best = WindowPlacementCalculator.FindBestWorkArea(
            new Point(100, 100),
            [],
            fallback);

        Assert.Equal(fallback, best);
    }
}
