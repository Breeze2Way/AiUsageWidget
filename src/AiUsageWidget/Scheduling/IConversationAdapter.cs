namespace AiUsageWidget.Scheduling;

public interface IConversationAdapter
{
    ConversationApp App { get; }
    Task EnsureReadyAsync(CancellationToken ct);
    // Historical name retained in the contract; opening the page is not required.
    Task<CreatedConversation> CreateAndOpenAsync(Guid runId, CancellationToken ct);
    Task SendAsync(CreatedConversation conversation, string text, CancellationToken ct);
    Task<bool> CanSafelyDeleteAsync(CreatedConversation conversation, CancellationToken ct);
    Task StopAndDeleteAsync(CreatedConversation conversation, CancellationToken ct);
}
