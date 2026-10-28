using System.IO;
using System.Text.Json;

namespace AiUsageWidget.Scheduling;

public sealed class AntigravityConversationAdapter : IConversationAdapter, IDisposable
{
    private readonly IConversationRpc rpc;
    private readonly Func<CancellationToken, Task> ready;
    public ConversationApp App => ConversationApp.Antigravity;
    public AntigravityConversationAdapter() : this(new AntigravityConversationRpc(), ct => DesktopAppLauncher.EnsureStartedAsync(ConversationApp.Antigravity, ct)) { }
    public AntigravityConversationAdapter(IConversationRpc rpc, Func<CancellationToken, Task> ready) { this.rpc = rpc; this.ready = ready; }
    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await ready(timeout.Token).ConfigureAwait(false);
        while (true)
        {
            try { await rpc.CallAsync("GetCapabilities", new { }, timeout.Token).ConfigureAwait(false); return; }
            catch (System.Net.Http.HttpRequestException) when (!timeout.IsCancellationRequested)
            { await Task.Delay(1000, timeout.Token).ConfigureAwait(false); }
        }
    }
    private async Task<JsonElement> GetModelAsync(CancellationToken ct)
    {
        var status = await rpc.CallAsync("GetUserStatus", new { }, ct).ConfigureAwait(false);
        var config = status.GetProperty("userStatus").GetProperty("cascadeModelConfigData");
        if (!config.TryGetProperty("defaultOverrideModelConfig", out var selected) ||
            !selected.TryGetProperty("modelOrAlias", out var model) || !model.TryGetProperty("model", out var value) ||
            value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)) throw new InvalidDataException("No default Antigravity model is available.");
        return model.Clone();
    }
    public async Task<CreatedConversation> CreateAndOpenAsync(Guid runId, CancellationToken ct)
    {
        var model = await GetModelAsync(ct).ConfigureAwait(false);
        var id = runId.ToString();
        try
        {
            await rpc.CallAsync("GetCascadeTrajectory", new { cascadeId = id }, ct).ConfigureAwait(false);
            throw new InvalidOperationException("Conversation ID already exists; not owned.");
        }
        catch (ConversationRpcException ex) when (ex.IsNotFound) { }
        var response = await rpc.CallAsync("StartCascade", new { cascadeId = id, source = 1, trajectoryType = 4, requestedModel = model.GetProperty("model"), tags = new[] { "AiUsageWidget-daily" } }, ct).ConfigureAwait(false);
        if (response.GetProperty("cascadeId").GetString() != id) throw new InvalidDataException("Mismatched created conversation ID.");
        return new(App, id, runId, DateTimeOffset.UtcNow);
    }
    public async Task SendAsync(CreatedConversation conversation, string text, CancellationToken ct)
    {
        Validate(conversation);
        var model = await GetModelAsync(ct).ConfigureAwait(false);
        await rpc.CallAsync("SendUserCascadeMessage", new
        {
            cascadeId = conversation.ThreadId, items = new[] { new { text } }, propagateError = true,
            cascadeConfig = new { plannerConfig = new { requestedModel = model } }
        }, ct).ConfigureAwait(false);
    }
    public async Task<bool> CanSafelyDeleteAsync(CreatedConversation conversation, CancellationToken ct)
    {
        Validate(conversation);
        try
        {
            var result = await rpc.CallAsync("GetCascadeTrajectory", new { cascadeId = conversation.ThreadId }, ct).ConfigureAwait(false);
            var trajectory = result.GetProperty("trajectory");
            if (trajectory.GetProperty("cascadeId").GetString() != conversation.ThreadId) return false;
            if (!trajectory.TryGetProperty("steps", out var steps)) return true;
            if (result.TryGetProperty("numTotalSteps", out var total) && total.GetInt32() > steps.GetArrayLength()) return false;
            var users = 0;
            foreach (var step in steps.EnumerateArray())
            {
                if (step.TryGetProperty("type", out var type) && (type.GetString() ?? "").Contains("SUBAGENT", StringComparison.OrdinalIgnoreCase)) return false;
                if (!step.TryGetProperty("userInput", out var input)) continue;
                if (++users > 1 || !input.TryGetProperty("userResponse", out var text) || text.GetString() != DailyConversationScheduler.Prompt) return false;
            }
            return true;
        }
        catch (ConversationRpcException ex) when (ex.IsNotFound) { return true; }
    }
    public async Task StopAndDeleteAsync(CreatedConversation conversation, CancellationToken ct)
    {
        Validate(conversation);
        if (!await CanSafelyDeleteAsync(conversation, ct).ConfigureAwait(false)) throw new InvalidOperationException("Conversation taken over; preserved.");
        try
        {
            await rpc.CallAsync("ForceStopCascadeTree", new { conversationId = conversation.ThreadId }, ct).ConfigureAwait(false);
            if (!await CanSafelyDeleteAsync(conversation, ct).ConfigureAwait(false)) throw new InvalidOperationException("Conversation taken over; preserved.");
            await rpc.CallAsync("DeleteCascadeTrajectory", new { cascadeId = conversation.ThreadId }, ct).ConfigureAwait(false);
        }
        catch (ConversationRpcException ex) when (ex.IsNotFound) { }
    }
    private void Validate(CreatedConversation c)
    {
        if (c.App != App || c.RunId == Guid.Empty || !Guid.TryParseExact(c.ThreadId, "D", out _)) throw new InvalidOperationException("Invalid conversation identity.");
    }
    public void Dispose() => rpc.Dispose();
}
