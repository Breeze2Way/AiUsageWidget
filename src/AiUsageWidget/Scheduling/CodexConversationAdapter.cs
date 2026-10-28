using System.IO;
using System.Text.Json;

namespace AiUsageWidget.Scheduling;

public sealed class CodexConversationAdapter : IConversationAdapter, IDisposable
{
    private readonly IConversationRpc rpc;
    private readonly Func<CancellationToken, Task> ready;
    public ConversationApp App => ConversationApp.Codex;
    public CodexConversationAdapter() : this(new CodexProcessRpc(), ct => ConversationCompatibilityGuard.EnsureInstalledReadyAsync(ConversationApp.Codex, ct)) { }
    public CodexConversationAdapter(IConversationRpc rpc, Func<CancellationToken, Task> ready) { this.rpc = rpc; this.ready = ready; }
    public Task EnsureReadyAsync(CancellationToken ct) => ready(ct);

    public async Task<CreatedConversation> CreateAndOpenAsync(Guid runId, CancellationToken ct)
    {
        var result = await rpc.CallAsync("thread/start", new
        {
            cwd = DesktopAppLauncher.WorkspacePath,
            approvalPolicy = "never", sandbox = "read-only", ephemeral = false, serviceName = "ai_usage_widget",
            config = new Dictionary<string, object> { ["web_search"] = "live", ["features.multi_agent"] = false },
            developerInstructions = "仅查询今日东京天气。不要修改文件、运行本地命令、创建子任务或访问与天气无关的数据。"
        }, ct).ConfigureAwait(false);
        var thread = result.GetProperty("thread");
        var id = thread.GetProperty("id").GetString();
        if (!Guid.TryParseExact(id, "D", out _) || thread.TryGetProperty("ephemeral", out var ep) && ep.ValueKind == JsonValueKind.True)
            throw new InvalidDataException("Invalid persistent conversation response.");
        return new(App, id!, runId, DateTimeOffset.UtcNow);
    }

    public async Task SendAsync(CreatedConversation conversation, string text, CancellationToken ct)
    {
        Validate(conversation);
        var response = await rpc.CallAsync("turn/start", new { threadId = conversation.ThreadId, input = new[] { new { type = "text", text } } }, ct).ConfigureAwait(false);
        if (!response.TryGetProperty("turn", out var turn) || !turn.TryGetProperty("id", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
            throw new InvalidDataException("Missing turn acknowledgement.");
    }

    public async Task<bool> CanSafelyDeleteAsync(CreatedConversation conversation, CancellationToken ct) =>
        await CheckDeletionSafetyAsync(conversation, ct).ConfigureAwait(false) != false;

    private async Task<bool?> CheckDeletionSafetyAsync(CreatedConversation conversation, CancellationToken ct)
    {
        Validate(conversation);
        try
        {
            var read = await rpc.CallAsync("thread/read", new { threadId = conversation.ThreadId, includeTurns = false }, ct).ConfigureAwait(false);
            var thread = read.GetProperty("thread");
            if (thread.GetProperty("id").GetString() != conversation.ThreadId ||
                thread.TryGetProperty("forkedFromId", out var parent) && parent.ValueKind != JsonValueKind.Null ||
                thread.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object) return false;
            var items = await rpc.CallAsync("thread/items/list", new { threadId = conversation.ThreadId, limit = 1000 }, ct).ConfigureAwait(false);
            if (items.TryGetProperty("nextCursor", out var cursor) && cursor.ValueKind != JsonValueKind.Null) return false;
            var users = 0;
            foreach (var wrapper in items.GetProperty("data").EnumerateArray())
            {
                var item = wrapper.GetProperty("item"); var type = item.GetProperty("type").GetString() ?? "";
                if (type.Contains("collab", StringComparison.OrdinalIgnoreCase) || type.Contains("subagent", StringComparison.OrdinalIgnoreCase)) return false;
                if (type != "userMessage") continue;
                if (++users > 1) return false;
                var content = item.GetProperty("content");
                if (content.GetArrayLength() != 1 || content[0].GetProperty("type").GetString() != "text" ||
                    content[0].GetProperty("text").GetString() != DailyConversationScheduler.Prompt) return false;
            }
            return true;
        }
        catch (ConversationRpcException ex) when (ex.IsNotFound) { return null; }
        catch (ConversationRpcException ex) when (ex.IsThreadNotLoaded)
        {
            // Not loaded does not mean deleted. Check every active and archived page.
            // History queries for missing threads can stall on the verified CLI version.
            if (await IsAbsentFromListsAsync(conversation.ThreadId, ct).ConfigureAwait(false)) return null;
            throw;
        }
    }

    private async Task<bool> IsAbsentFromListsAsync(string threadId, CancellationToken ct)
    {
        foreach (var archived in new[] { false, true })
        {
            string? cursor = null;
            var seen = new HashSet<string>();
            do
            {
                var page = await rpc.CallAsync("thread/list", new { limit = 100, sourceKinds = Array.Empty<string>(), archived, cursor }, ct).ConfigureAwait(false);
                if (page.GetProperty("data").EnumerateArray().Any(t => t.GetProperty("id").GetString() == threadId)) return false;
                cursor = page.GetProperty("nextCursor").GetString();
                if (cursor is not null && !seen.Add(cursor)) throw new InvalidDataException("Repeated thread-list cursor.");
            } while (cursor is not null);
        }
        return true;
    }

    public async Task StopAndDeleteAsync(CreatedConversation conversation, CancellationToken ct)
    {
        Validate(conversation);
        var safety = await CheckDeletionSafetyAsync(conversation, ct).ConfigureAwait(false);
        if (safety is null) return;
        if (safety == false) throw new InvalidOperationException("Conversation taken over; preserved.");
        try
        {
            var turns = await rpc.CallAsync("thread/turns/list", new { threadId = conversation.ThreadId, limit = 1000 }, ct).ConfigureAwait(false);
            if (turns.TryGetProperty("nextCursor", out var cursor) && cursor.ValueKind != JsonValueKind.Null) throw new InvalidOperationException("Incomplete history.");
            foreach (var turn in turns.GetProperty("data").EnumerateArray())
                if (turn.GetProperty("status").GetString() == "inProgress")
                    await rpc.CallAsync("turn/interrupt", new { threadId = conversation.ThreadId, turnId = turn.GetProperty("id").GetString() }, ct).ConfigureAwait(false);
            if (!await CanSafelyDeleteAsync(conversation, ct).ConfigureAwait(false)) throw new InvalidOperationException("Conversation taken over; preserved.");
            await rpc.CallAsync("thread/delete", new { threadId = conversation.ThreadId }, ct).ConfigureAwait(false);
        }
        catch (ConversationRpcException ex) when (ex.IsNotFound) { }
    }
    private void Validate(CreatedConversation c)
    {
        if (c.App != App || c.RunId == Guid.Empty || !Guid.TryParseExact(c.ThreadId, "D", out _)) throw new InvalidOperationException("Invalid conversation identity.");
    }
    public void Dispose() => rpc.Dispose();
}
