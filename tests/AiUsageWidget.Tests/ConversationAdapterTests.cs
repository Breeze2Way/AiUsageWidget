using System.Text.Json;
using AiUsageWidget.Scheduling;

namespace AiUsageWidget.Tests;

public sealed class ConversationAdapterTests
{
    private static readonly Guid RunId = Guid.Parse("9321f5eb-387b-4ab0-becf-d8204b64e452");
    private const string ThreadId = "01a180b5-9a42-7cc3-98a9-ab3cced3b4bb";
    private static CreatedConversation Owned(ConversationApp app) => new(app, ThreadId, RunId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task CodexCreatesPersistentReadOnlyRootWithoutHardcodedModelAndSendsOnce()
    {
        var rpc = new FakeRpc((method, _) => method == "thread/start" ? JsonSerializer.SerializeToElement(new { thread = new { id = ThreadId, ephemeral = false } }) : J("{\"turn\":{\"id\":\"turn-1\"}}"));
        var adapter = new CodexConversationAdapter(rpc, _ => Task.CompletedTask);
        var conversation = await adapter.CreateAndOpenAsync(RunId, default);
        await adapter.SendAsync(conversation, DailyConversationScheduler.Prompt, default);
        var create = rpc.Calls[0].Params;
        Assert.Equal("read-only", create.GetProperty("sandbox").GetString());
        Assert.Equal("never", create.GetProperty("approvalPolicy").GetString());
        Assert.False(create.GetProperty("ephemeral").GetBoolean());
        Assert.False(create.TryGetProperty("model", out _));
        var send = Assert.Single(rpc.Calls, x => x.Method == "turn/start").Params;
        Assert.Equal(ThreadId, send.GetProperty("threadId").GetString());
        Assert.Equal("今日东京天气", send.GetProperty("input")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(0, false, false, true)]
    [InlineData(1, false, false, true)]
    [InlineData(2, false, false, false)]
    [InlineData(1, true, false, false)]
    [InlineData(1, false, true, false)]
    public async Task CodexChecksWrappedUserMessagesPaginationAndChildCalls(int messages, bool child, bool paginated, bool expected)
    {
        var items = Enumerable.Range(0, messages).Select(_ => new { turnId = "turn", item = new { type = "userMessage", content = new[] { new { type = "text", text = "今日东京天气" } } } }).Cast<object>().ToList();
        if (child) items.Add(new { turnId = "turn", item = new { type = "collabAgentToolCall" } });
        var rpc = new FakeRpc((method, _) => method == "thread/read" ? JsonSerializer.SerializeToElement(new { thread = new { id = ThreadId } }) : JsonSerializer.SerializeToElement(new { data = items, nextCursor = paginated ? "more" : null }));
        Assert.Equal(expected, await new CodexConversationAdapter(rpc, _ => Task.CompletedTask).CanSafelyDeleteAsync(Owned(ConversationApp.Codex), default));
    }

    [Fact]
    public async Task CodexDoesNotDeleteAfterManualTakeoverBetweenChecks()
    {
        var rpc = new FakeRpc((method, _) => method switch
        {
            "thread/read" => JsonSerializer.SerializeToElement(new { thread = new { id = ThreadId } }),
            "thread/items/list" => J("{\"data\":[{\"item\":{\"type\":\"userMessage\",\"content\":[{\"type\":\"text\",\"text\":\"manual work\"}]}}]}"),
            _ => J("{}")
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CodexConversationAdapter(rpc, _ => Task.CompletedTask).StopAndDeleteAsync(Owned(ConversationApp.Codex), default));
        Assert.DoesNotContain(rpc.Calls, x => x.Method == "thread/delete");
    }

    [Fact]
    public async Task AntigravityUsesServerReturnedDefaultModelAndClientChosenFreshId()
    {
        var rpc = new FakeRpc((method, args) => method switch
        {
            "GetUserStatus" => J("{\"userStatus\":{\"cascadeModelConfigData\":{\"defaultOverrideModelConfig\":{\"modelOrAlias\":{\"model\":\"MODEL_FROM_SERVER\"}}}}}"),
            "StartCascade" => JsonSerializer.SerializeToElement(new { cascadeId = args.GetProperty("cascadeId").GetString() }),
            "GetCascadeTrajectory" => throw new ConversationRpcException("not_found", "trajectory not found"),
            _ => J("{}")
        });
        var adapter = new AntigravityConversationAdapter(rpc, _ => Task.CompletedTask);
        var conversation = await adapter.CreateAndOpenAsync(RunId, default);
        await adapter.SendAsync(conversation, DailyConversationScheduler.Prompt, default);
        Assert.Equal(RunId.ToString(), conversation.ThreadId);
        var send = Assert.Single(rpc.Calls, x => x.Method == "SendUserCascadeMessage").Params;
        Assert.Equal("今日东京天气", send.GetProperty("items")[0].GetProperty("text").GetString());
        Assert.Equal("MODEL_FROM_SERVER", send.GetProperty("cascadeConfig").GetProperty("plannerConfig").GetProperty("requestedModel").GetProperty("model").GetString());
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task AntigravityPreservesExtraHumanInput(int count, bool expected)
    {
        var steps = Enumerable.Range(0, count).Select(_ => new { type = "CORTEX_STEP_TYPE_USER_INPUT", userInput = new { userResponse = "今日东京天气" } });
        var rpc = new FakeRpc((_, _) => JsonSerializer.SerializeToElement(new { trajectory = new { cascadeId = ThreadId, steps }, numTotalSteps = count }));
        Assert.Equal(expected, await new AntigravityConversationAdapter(rpc, _ => Task.CompletedTask).CanSafelyDeleteAsync(Owned(ConversationApp.Antigravity), default));
    }

    [Fact]
    public async Task InvalidIdentityCannotReachDeleteOrSend()
    {
        var rpc = new FakeRpc((_, _) => J("{}"));
        var adapter = new CodexConversationAdapter(rpc, _ => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.SendAsync(Owned(ConversationApp.Antigravity), "今日东京天气", default));
        Assert.Empty(rpc.Calls);
    }

    [Theory]
    [InlineData("{\"numTotalSteps\":5,\"trajectory\":{\"cascadeId\":\"01a180b5-9a42-7cc3-98a9-ab3cced3b4bb\"}}")]
    [InlineData("{\"trajectory\":{\"cascadeId\":\"01a180b5-9a42-7cc3-98a9-ab3cced3b4bb\",\"steps\":[{\"type\":\"CORTEX_STEP_TYPE_USER_INPUT\"}]}}")]
    public async Task AntigravityMalformedOrTruncatedHistoryIsPreserved(string json)
    {
        var rpc = new FakeRpc((_, _) => J(json));
        Assert.False(await new AntigravityConversationAdapter(rpc, _ => Task.CompletedTask).CanSafelyDeleteAsync(Owned(ConversationApp.Antigravity), default));
    }

    [Fact]
    public async Task RemoteFailureCannotReportSendSuccess()
    {
        var rpc = new FakeRpc((_, _) => throw new ConversationRpcException("unauthenticated", "secret response not logged"));
        await Assert.ThrowsAsync<ConversationRpcException>(() => new CodexConversationAdapter(rpc, _ => Task.CompletedTask).SendAsync(Owned(ConversationApp.Codex), "今日东京天气", default));
    }

    private static JsonElement J(string text) => JsonDocument.Parse(text).RootElement.Clone();
    private sealed class FakeRpc(Func<string, JsonElement, JsonElement> handler) : IConversationRpc
    {
        public readonly List<(string Method, JsonElement Params)> Calls = [];
        public Task<JsonElement> CallAsync(string method, object args, CancellationToken ct)
        {
            var json = JsonSerializer.SerializeToElement(args); Calls.Add((method, json));
            return Task.FromResult(handler(method, json));
        }
        public void Dispose() { }
    }
}
