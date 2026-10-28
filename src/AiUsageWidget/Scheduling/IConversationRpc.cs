using System.Text.Json;

namespace AiUsageWidget.Scheduling;

public interface IConversationRpc : IDisposable
{
    Task<JsonElement> CallAsync(string method, object args, CancellationToken ct);
}

public sealed class ConversationRpcException(string code, string remoteMessage) : Exception("Conversation service rejected the request.")
{
    public string Code { get; } = code;
    public bool IsThreadNotLoaded => Code == "-32600" && remoteMessage.StartsWith("thread not loaded:", StringComparison.OrdinalIgnoreCase);
    // Remote bodies are intentionally excluded from Exception.Message / logs.
    public bool IsNotFound => Code is "unknown" or "not_found" or "-32600" or "-32000" &&
        (remoteMessage.StartsWith("thread not found", StringComparison.OrdinalIgnoreCase) ||
         remoteMessage.StartsWith("trajectory not found", StringComparison.OrdinalIgnoreCase) ||
         remoteMessage.StartsWith("no rollout found for thread id", StringComparison.OrdinalIgnoreCase));
}
