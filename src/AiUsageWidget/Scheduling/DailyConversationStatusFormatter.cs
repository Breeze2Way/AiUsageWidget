using AiUsageWidget.Models;

namespace AiUsageWidget.Scheduling;

public static class DailyConversationStatusFormatter
{
    public static DateTime? NextRunLocal(WidgetSettings settings, IReadOnlyList<ConversationRun> runs, DateTimeOffset now)
    {
        if (!settings.DailyConversationEnabled || !DailyConversationOptions.IsValid(settings) ||
            !DailyConversationOptions.TryParseTime(settings.DailyConversationTime, out var time)) return null;
        var today = DateOnly.FromDateTime(now.Date);
        var due = today.ToDateTime(time);
        var selected = new[] { ConversationApp.Codex, ConversationApp.Antigravity }.Where(a =>
            a == ConversationApp.Codex ? settings.DailyConversationCodex : settings.DailyConversationAntigravity);
        if (now.DateTime >= due.AddMinutes(1) || selected.All(a => runs.Any(r => r.App == a && r.ScheduledDate == today))) due = due.AddDays(1);
        return due;
    }
    public static string Result(string code, bool english) => (code, english) switch
    {
        ("reserved", false) => "准备执行", ("reserved", true) => "Preparing",
        ("created", false) => "会话已创建", ("created", true) => "Created",
        ("sending", false) => "正在发送", ("sending", true) => "Sending",
        ("waiting_cleanup", false) => "已发送，等待清理", ("waiting_cleanup", true) => "Sent; Waiting for cleanup",
        ("send_uncertain", false) => "发送结果不明，等待清理（不重发）", ("send_uncertain", true) => "Send uncertain; cleanup pending (no resend)",
        ("deleted", false) => "临时会话已删除", ("deleted", true) => "Deleted temporary conversation",
        ("cleanup_skipped", false) => "已保留：检测到人工消息或无法确认安全", ("cleanup_skipped", true) => "Preserved: human input or unsafe to delete",
        ("cleanup_failed", false) => "清理失败，待重试；重启后可恢复", ("cleanup_failed", true) => "Cleanup failed; pending retry/restart recovery",
        ("create_failed", false) => "启动/创建失败：请检查安装、登录及接口版本", ("create_failed", true) => "Startup/create failed: check installation, sign-in and version",
        ("interrupted", false) => "上次执行中断，未重发", ("interrupted", true) => "Interrupted; not resent",
        (_, false) => "尚未执行", _ => "Not run yet"
    };
}
