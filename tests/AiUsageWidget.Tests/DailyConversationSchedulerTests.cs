using AiUsageWidget.Models;
using AiUsageWidget.Scheduling;

namespace AiUsageWidget.Tests;

public sealed class DailyConversationSchedulerTests
{
    private static WidgetSettings Enabled => new() { DailyConversationEnabled = true };

    [Fact]
    public async Task DueMinuteRunsEachAppOnceAndRestartDoesNotRepeat()
    {
        var f = new Fixture();
        await f.Scheduler.TickAsync(Enabled, default);
        await f.Scheduler.TickAsync(Enabled, default);
        await new DailyConversationScheduler(f.Store, f.Adapters, f.Clock).TickAsync(Enabled, default);
        Assert.Equal(new[] { "今日东京天气" }, f.Codex.Messages);
        Assert.Equal(new[] { "今日东京天气" }, f.Antig.Messages);
        Assert.Equal(2, f.Store.Runs.Count);
    }

    [Fact]
    public async Task DisabledAndMissedMinuteDoNotCreate()
    {
        var f = new Fixture();
        await f.Scheduler.TickAsync(new(), default);
        f.Clock.Advance(60);
        await f.Scheduler.TickAsync(Enabled, default);
        Assert.Empty(f.Store.Runs);
    }

    [Fact]
    public async Task SelectedAppsAndFailureAreIndependentOfDisplay()
    {
        var f = new Fixture(); f.Codex.FailReady = true;
        await f.Scheduler.TickAsync(Enabled with { ShowCodex = false, ShowAntigravity = false }, default);
        Assert.Empty(f.Codex.Messages);
        Assert.Single(f.Antig.Messages);
        var g = new Fixture();
        await g.Scheduler.TickAsync(Enabled with { DailyConversationAntigravity = false }, default);
        Assert.Empty(g.Antig.Messages); Assert.Single(g.Codex.Messages);
    }

    [Fact]
    public async Task DoesNotDeleteBeforeSixtySecondsAndDisabledStillCleans()
    {
        var f = new Fixture();
        await f.Scheduler.TickAsync(Enabled, default);
        f.Clock.Advance(59); await f.Scheduler.TickAsync(new(), default);
        Assert.Empty(f.Codex.Deleted);
        f.Clock.Advance(1); await f.Scheduler.TickAsync(new(), default);
        Assert.Single(f.Codex.Deleted);
        Assert.All(f.Store.Runs, r => Assert.Equal(ConversationRunStage.Completed, r.Stage));
    }

    [Fact]
    public async Task RestartResumesCleanupWithoutSending()
    {
        var f = new Fixture(); await f.Scheduler.TickAsync(Enabled, default);
        f.Clock.Advance(65);
        await new DailyConversationScheduler(f.Store, f.Adapters, f.Clock).TickAsync(new(), default);
        Assert.Single(f.Codex.Messages); Assert.Single(f.Codex.Deleted);
    }

    [Fact]
    public async Task HumanTakeoverPreservesConversation()
    {
        var f = new Fixture(); await f.Scheduler.TickAsync(Enabled, default);
        f.Codex.Safe = false; f.Clock.Advance(60);
        await f.Scheduler.TickAsync(new(), default);
        Assert.Empty(f.Codex.Deleted);
        Assert.Equal(ConversationRunStage.Preserved, f.Store.Runs.Single(r => r.App == ConversationApp.Codex).Stage);
    }

    [Fact]
    public async Task AmbiguousSendIsNotRetriedAndCleanupFailureRemainsPending()
    {
        var f = new Fixture(); f.Codex.FailSend = true;
        await f.Scheduler.TickAsync(Enabled, default);
        f.Codex.FailDelete = true; f.Clock.Advance(60);
        await f.Scheduler.TickAsync(Enabled, default);
        Assert.Single(f.Codex.Messages);
        Assert.Equal(ConversationRunStage.CleanupPending, f.Store.Runs.Single(r => r.App == ConversationApp.Codex).Stage);
        f.Codex.FailDelete = false; f.Clock.Advance(15);
        await f.Scheduler.TickAsync(Enabled, default);
        Assert.Single(f.Codex.Deleted);
    }

    [Fact]
    public async Task ReserveWriteFailureDoesNotLaunchAndCorruptHistoryStopsAllActions()
    {
        var f = new Fixture(); f.Store.FailSave = true;
        await f.Scheduler.TickAsync(Enabled, default);
        Assert.Equal(0, f.Codex.ReadyCalls); Assert.Equal(0, f.Antig.ReadyCalls);
        var g = new Fixture(); g.Store.FailLoad = true;
        await new DailyConversationScheduler(g.Store, g.Adapters, g.Clock).TickAsync(Enabled, default);
        Assert.Empty(g.Codex.Messages); Assert.Empty(g.Antig.Messages);
    }

    [Fact]
    public async Task OwnershipWriteFailureDoesNotSend()
    {
        var f = new Fixture(); f.Codex.OnCreate = () => f.Store.FailSave = true;
        await f.Scheduler.TickAsync(Enabled with { DailyConversationAntigravity = false }, default);
        Assert.Empty(f.Codex.Messages);
        Assert.NotNull(f.Scheduler.FaultCode);
        Assert.NotNull(f.Scheduler.UnpersistedConversation);
    }

    [Fact]
    public async Task WallClockForwardDoesNotDeleteBeforeSixtyRealSeconds()
    {
        var f = new Fixture(); await f.Scheduler.TickAsync(Enabled, default);
        f.Clock.ChangeWallClock(3600);
        await f.Scheduler.TickAsync(new(), default);
        Assert.Empty(f.Codex.Deleted);
        f.Clock.Advance(60); await f.Scheduler.TickAsync(new(), default);
        Assert.Single(f.Codex.Deleted);
    }

    [Fact]
    public async Task ManualDraftTimeDoesNotSuppressSavedSchedule()
    {
        var f = new Fixture(); f.Clock.Advance(-60);
        await f.Scheduler.TickAsync(new(), default);
        await f.Scheduler.TestNowAsync(Enabled with { DailyConversationTime = "06:00" }, default);
        f.Clock.Advance(60); await f.Scheduler.TickAsync(Enabled, default);
        Assert.Equal(2, f.Codex.Messages.Count);
    }

    [Fact]
    public async Task ManualTestDoesNotConsumeDailyRunAndConcurrentTicksDoNotDuplicate()
    {
        var f = new Fixture();
        await Task.WhenAll(f.Scheduler.TestNowAsync(Enabled, default), f.Scheduler.TickAsync(Enabled, default));
        Assert.Single(f.Codex.Messages);
        f.Clock.Advance(60); await f.Scheduler.TickAsync(new(), default);
        f.Clock.Advance(24 * 3600 - 60); await f.Scheduler.TickAsync(Enabled, default);
        Assert.Equal(2, f.Codex.Messages.Count);
    }

    [Fact]
    public async Task TimeChangeDoesNotRunImmediatelyAndClockRollbackDoesNotDuplicate()
    {
        var f = new Fixture();
        await f.Scheduler.TickAsync(Enabled with { DailyConversationTime = "06:00" }, default);
        await f.Scheduler.TickAsync(Enabled, default);
        Assert.Empty(f.Codex.Messages);
        f.Clock.Advance(24 * 3600); await f.Scheduler.TickAsync(Enabled, default);
        f.Clock.Advance(60); await f.Scheduler.TickAsync(new(), default);
        f.Clock.Advance(-60); await f.Scheduler.TickAsync(Enabled, default);
        Assert.Single(f.Codex.Messages);
    }

    [Fact]
    public async Task CancellationRetainsKnownIdForCleanup()
    {
        var f = new Fixture(); f.Codex.FailSend = true;
        await f.Scheduler.TickAsync(Enabled with { DailyConversationAntigravity = false }, default);
        var run = Assert.Single(f.Store.Runs);
        Assert.NotNull(run.Conversation); Assert.NotNull(run.CleanupDueAt);
        f.Clock.Advance(60); await new DailyConversationScheduler(f.Store, f.Adapters, f.Clock).TickAsync(new(), default);
        Assert.Single(f.Codex.Deleted);
    }

    private sealed class Fixture
    {
        public readonly TestClock Clock = new();
        public readonly MemoryStore Store = new();
        public readonly FakeAdapter Codex = new(ConversationApp.Codex);
        public readonly FakeAdapter Antig = new(ConversationApp.Antigravity);
        public IConversationAdapter[] Adapters => [Codex, Antig];
        public DailyConversationScheduler Scheduler { get; }
        public Fixture() => Scheduler = new(Store, Adapters, Clock);
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.Parse("2026-10-28T05:30:00+08:00");
        private long timestamp;
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override long GetTimestamp() => timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        public void Advance(int seconds) { now = now.AddSeconds(seconds); timestamp += Math.Max(0, seconds) * TimeSpan.TicksPerSecond; }
        public void ChangeWallClock(int seconds) => now = now.AddSeconds(seconds);
    }
    private sealed class MemoryStore : IConversationRunStore
    {
        public List<ConversationRun> Runs = [];
        public bool FailSave, FailLoad;
        public IReadOnlyList<ConversationRun> Load() => FailLoad ? throw new InvalidDataException() : Runs.ToArray();
        public void Save(IReadOnlyList<ConversationRun> runs)
        {
            if (FailSave) throw new IOException();
            ConversationRunStore.Validate(runs); Runs = runs.ToList();
        }
    }
    private sealed class FakeAdapter(ConversationApp app) : IConversationAdapter
    {
        public ConversationApp App => app;
        public List<string> Messages = [], Deleted = [];
        public bool FailReady, FailSend, FailDelete, Safe = true;
        public int ReadyCalls;
        public Action? OnCreate;
        public Task EnsureReadyAsync(CancellationToken ct) { ReadyCalls++; return FailReady ? Task.FromException(new IOException()) : Task.CompletedTask; }
        public Task<CreatedConversation> CreateAndOpenAsync(Guid id, CancellationToken ct)
        {
            OnCreate?.Invoke(); return Task.FromResult(new CreatedConversation(app, Guid.NewGuid().ToString(), id, DateTimeOffset.UtcNow));
        }
        public Task SendAsync(CreatedConversation c, string text, CancellationToken ct)
        { Messages.Add(text); return FailSend ? Task.FromException(new OperationCanceledException()) : Task.CompletedTask; }
        public Task<bool> CanSafelyDeleteAsync(CreatedConversation c, CancellationToken ct) => Task.FromResult(Safe);
        public Task StopAndDeleteAsync(CreatedConversation c, CancellationToken ct)
        { if (FailDelete) throw new IOException(); Deleted.Add(c.ThreadId); return Task.CompletedTask; }
    }
}
