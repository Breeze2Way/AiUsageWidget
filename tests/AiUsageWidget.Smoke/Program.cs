using System.IO;
using System.Text.Json;
using AiUsageWidget.Scheduling;

// Explicit opt-in integration harness. Never run as part of dotnet test.
if (args.Length != 2 || args[0] != "--live-create-send-delete")
{
    Console.Error.WriteLine("Requires --live-create-send-delete <ownership-json-path>; sends real requests and deletes only test-created IDs.");
    return 2;
}
var owned = new List<ConversationRun>();
using var codex = new CodexConversationAdapter();
using var antig = new AntigravityConversationAdapter();
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var token = cancellation.Token;
foreach (var adapter in new IConversationAdapter[] { codex, antig })
{
    var runId = Guid.NewGuid();
    await adapter.EnsureReadyAsync(token);
    var conversation = await adapter.CreateAndOpenAsync(runId, token);
    var record = new ConversationRun(runId, adapter.App, null) { Conversation = conversation, Stage = ConversationRunStage.Created };
    owned.Add(record);
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(owned), token);
    Console.WriteLine($"CREATED {adapter.App} {conversation.ThreadId}");
    await adapter.SendAsync(conversation, DailyConversationScheduler.Prompt, token);
    owned[^1] = record with { Stage = ConversationRunStage.CleanupPending, SentAt = DateTimeOffset.UtcNow, CleanupDueAt = DateTimeOffset.UtcNow.AddSeconds(60) };
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(owned), token);
    Console.WriteLine($"SENT {adapter.App}");
}
await Task.Delay(TimeSpan.FromSeconds(61), token);
foreach (var adapter in new IConversationAdapter[] { codex, antig })
{
    var run = owned.Single(r => r.App == adapter.App);
    if (!await adapter.CanSafelyDeleteAsync(run.Conversation!, token)) throw new InvalidOperationException($"{adapter.App}: preserve because ownership/content could not be confirmed.");
    await adapter.StopAndDeleteAsync(run.Conversation!, token);
    owned[owned.IndexOf(run)] = run with { Stage = ConversationRunStage.Completed, ResultCode = "deleted" };
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(owned), token);
    Console.WriteLine($"DELETED {adapter.App}");
}
return 0;
