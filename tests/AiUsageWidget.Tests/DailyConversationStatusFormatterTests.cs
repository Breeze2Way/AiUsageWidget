using AiUsageWidget.Models;
using AiUsageWidget.Scheduling;

namespace AiUsageWidget.Tests;

public sealed class DailyConversationStatusFormatterTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-28T05:29:00+08:00");
    [Fact]
    public void DisabledHasNoNextRunAndFutureScheduleUsesLocalDate()
    {
        Assert.Null(DailyConversationStatusFormatter.NextRunLocal(new(), [], Now));
        var settings = new WidgetSettings { DailyConversationEnabled = true };
        Assert.Equal(new DateTime(2026, 10, 28, 5, 30, 0), DailyConversationStatusFormatter.NextRunLocal(settings, [], Now));
        Assert.Equal(new DateTime(2026, 10, 29, 5, 30, 0), DailyConversationStatusFormatter.NextRunLocal(settings, [], Now.AddMinutes(3)));
    }
    [Fact]
    public void BothAppsAlreadyAttemptedTodayShowsTomorrow()
    {
        var settings = new WidgetSettings { DailyConversationEnabled = true };
        ConversationRun[] runs = [new(Guid.NewGuid(), ConversationApp.Codex, new(2026, 10, 28)), new(Guid.NewGuid(), ConversationApp.Antigravity, new(2026, 10, 28))];
        Assert.Equal(new DateTime(2026, 10, 29, 5, 30, 0), DailyConversationStatusFormatter.NextRunLocal(settings, runs, Now));
        Assert.Equal(new DateTime(2026, 10, 28, 5, 30, 0), DailyConversationStatusFormatter.NextRunLocal(settings, runs.Take(1).ToArray(), Now));
    }
    [Theory]
    [InlineData("waiting_cleanup", "等待清理", "Waiting for cleanup")]
    [InlineData("cleanup_skipped", "已保留", "Preserved")]
    [InlineData("send_uncertain", "发送结果不明", "Send uncertain")]
    [InlineData("deleted", "已删除", "Deleted")]
    public void StatusTranslationExplainsWhatHappened(string code, string chinese, string english)
    {
        Assert.Contains(chinese, DailyConversationStatusFormatter.Result(code, false));
        Assert.Contains(english, DailyConversationStatusFormatter.Result(code, true));
    }
}
