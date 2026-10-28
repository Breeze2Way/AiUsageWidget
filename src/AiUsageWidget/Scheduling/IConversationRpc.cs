using System.Text.Json;

namespace AiUsageWidget.Scheduling;

public interface IConversationRpc : IDisposable
{
    Task<JsonElement> CallAsync(string method, object args, CancellationToken ct);
}

public sealed class ConversationRpcException(string code, string remoteMessage) : Exception("Conversation service rejected the request.")
{
    public string Code { get; } = code;
    // Remote bodies are intentionally excluded from Exception.Message / logs.
    public bool IsNotFound => Code == "not_found" || Code == "404" ||
        (remoteMessage.Contains("not found", StringComparison.OrdinalIgnoreCase) &&
         (remoteMessage.Contains("thread", StringComparison.OrdinalIgnoreCase) || remoteMessage.Contains("trajectory", StringComparison.OrdinalIgnoreCase)));
}
