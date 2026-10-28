using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Scheduling;

namespace AiUsageWidget.Tests;

public sealed class ConversationSettingsTests
{
    [Fact]
    public void UpgradeDefaultsDoNotEnableSending()
    {
        var settings = new WidgetSettings();
        Assert.False(settings.DailyConversationEnabled);
        Assert.Equal("05:30", settings.DailyConversationTime);
        Assert.True(settings.DailyConversationCodex);
        Assert.True(settings.DailyConversationAntigravity);
    }

    [Theory]
    [InlineData("05:30", "05:30")]
    [InlineData("5:30", "05:30")]
    [InlineData("23:59", "23:59")]
    [InlineData("24:00", "05:30")]
    [InlineData("05:30:00", "05:30")]
    [InlineData("invalid", "05:30")]
    [InlineData(null, "05:30")]
    public void NormalizeTime(string? input, string expected) =>
        Assert.Equal(expected, SettingsStore.Normalize(new WidgetSettings { DailyConversationTime = input! }).DailyConversationTime);

    [Fact]
    public void EnabledRequiresAppButDisplaySwitchesAreIndependent()
    {
        var settings = new WidgetSettings { DailyConversationEnabled = true, DailyConversationCodex = false, DailyConversationAntigravity = false };
        Assert.False(DailyConversationOptions.IsValid(settings));
        Assert.True(DailyConversationOptions.IsValid(settings with { DailyConversationEnabled = false }));
        Assert.True(DailyConversationOptions.IsValid(settings with { DailyConversationCodex = true, ShowCodex = false, ShowAntigravity = false }));
        Assert.False(DailyConversationOptions.IsValid(settings with { DailyConversationCodex = true, DailyConversationTime = "bad" }));
    }

    [Fact]
    public void RoundTripsSchedule()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var store = new SettingsStore(dir);
            var settings = new WidgetSettings { DailyConversationEnabled = true, DailyConversationTime = "06:45", DailyConversationCodex = false };
            store.Save(settings);
            Assert.Equal(settings, store.Load());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
