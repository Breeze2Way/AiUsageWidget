namespace AiUsageWidget.Scheduling;

public enum ConversationApp { Codex, Antigravity }
public enum ConversationRunStage { Reserved, Created, Sending, CleanupPending, Completed, Failed, Preserved }
public sealed record CreatedConversation(ConversationApp App, string ThreadId, Guid RunId, DateTimeOffset CreatedAt);
public sealed record ConversationRun(Guid RunId, ConversationApp App, DateOnly? ScheduledDate)
{
    public ConversationRunStage Stage { get; init; } = ConversationRunStage.Reserved;
    public CreatedConversation? Conversation { get; init; }
    public DateTimeOffset? SendStartedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public DateTimeOffset? CleanupDueAt { get; init; }
    public int CleanupAttempts { get; init; }
    public DateTimeOffset? RetryAt { get; init; }
    public string ResultCode { get; init; } = "reserved";
    public string? CleanupErrorCode { get; init; }
}
