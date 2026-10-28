using AiUsageWidget.Scheduling;

namespace AiUsageWidget.Tests;

public sealed class ConversationRunStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "AiUsageWidgetTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(directory, "conversation-runs.json");

    [Fact]
    public void RoundTripsOwnedRunAndEmptyStore()
    {
        var store = new ConversationRunStore(directory);
        Assert.Empty(store.Load());
        var run = NewRun();
        store.Save([run]);
        Assert.Equal(run, Assert.Single(store.Load()));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("null")]
    [InlineData("{\"Version\":99,\"Runs\":[]}")]
    public void CorruptStateIsNotAnEmptyHistory(string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, json);
        Assert.Throws<InvalidDataException>(() => new ConversationRunStore(directory).Load());
    }

    [Fact]
    public void RejectsForeignIdentityAndInvalidIds()
    {
        var store = new ConversationRunStore(directory);
        var run = NewRun();
        Assert.Throws<InvalidDataException>(() => store.Save([run with { Conversation = run.Conversation! with { RunId = Guid.NewGuid() } }]));
        Assert.Throws<InvalidDataException>(() => store.Save([run with { Conversation = run.Conversation! with { ThreadId = "--last" } }]));
        Assert.Throws<InvalidDataException>(() => store.Save([run, run]));
    }

    [Fact]
    public void FailedSavePreservesPreviousHistory()
    {
        var store = new ConversationRunStore(directory);
        var run = NewRun();
        store.Save([run]);
        Assert.Throws<InvalidDataException>(() => store.Save([run with { RunId = Guid.Empty }]));
        Assert.Equal(run, Assert.Single(store.Load()));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    private static ConversationRun NewRun()
    {
        var id = Guid.NewGuid();
        var time = DateTimeOffset.Parse("2026-10-28T05:30:00+08:00");
        return new ConversationRun(id, ConversationApp.Codex, new DateOnly(2026, 10, 28))
        {
            Stage = ConversationRunStage.CleanupPending,
            Conversation = new(ConversationApp.Codex, "01a180b5-9a42-7cc3-98a9-ab3cced3b4bb", id, time),
            SentAt = time, CleanupDueAt = time.AddMinutes(1)
        };
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
