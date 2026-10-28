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
    public async Task DisableDuringReadinessPreventsNewCreation()
    {
        var f = new Fixture();
        var current = Enabled with { DailyConversationAntigravity = false };
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Codex.OnReadyAsync = () => ready.Task;
        var scheduler = new DailyConversationScheduler(f.Store, f.Adapters, f.Clock, () => current);
        var tick = scheduler.TickAsync(current, default);
        current = current with { DailyConversationEnabled = false };
        ready.SetResult(); await tick;
        Assert.Equal(0, f.Codex.CreatedCalls);
        Assert.Empty(f.Codex.Messages);
    }

    [Fact]
    public async Task SlowCleanupRechecksTimeBeforeCreating()
    {
        var f = new Fixture(); f.Clock.Advance(-10);
        await f.Scheduler.TestNowAsync(Enabled with { DailyConversationAntigravity = false }, default);
        f.Clock.Advance(60);
        f.Codex.OnReadyAsync = () => { f.Clock.Advance(20); return Task.CompletedTask; };
        await f.Scheduler.TickAsync(Enabled with { DailyConversationAntigravity = false }, default);
        Assert.Single(f.Codex.Messages);
    }

    [Fact]
    public async Task SlowProviderRecoveryDoesNotBlockOtherProvidersDueMinute()
    {
        var f = new Fixture(); f.Clock.Advance(-120);
        await f.Scheduler.TestNowAsync(Enabled with { DailyConversationAntigravity = false }, default);
        f.Clock.Advance(60);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Codex.OnReadyAsync = () => ready.Task;
        var recovering = f.Scheduler.TickAsync(Enabled, default);
        f.Clock.Advance(60);
        await f.Scheduler.TickAsync(Enabled, default);
        Assert.Single(f.Antig.Messages);
        ready.SetResult(); await recovering;
    }

    [Fact]
    public async Task ConcurrentOwnershipFailuresRetainEveryKnownOrphan()
    {
        var f = new Fixture();
        var create = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Codex.OnCreateAsync = f.Antig.OnCreateAsync = () => create.Task;
        var tick = f.Scheduler.TickAsync(Enabled, default);
        f.Store.FailSave = true; create.SetResult(); await tick;
        Assert.Equal(2, f.Scheduler.UnpersistedConversations.Count);
        Assert.Empty(f.Codex.Messages); Assert.Empty(f.Antig.Messages);
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

    [Fact]
    public async Task ActualCancellationOfBlockedSendKeepsRecoverableOwnership()
    {
        var f = new Fixture(); using var cancellation = new CancellationTokenSource();
        f.Codex.OnSendAsync = ct => Task.Delay(Timeout.Infinite, ct);
        var tick = f.Scheduler.TickAsync(Enabled with { DailyConversationAntigravity = false }, cancellation.Token);
        cancellation.Cancel(); await tick;
        var run = Assert.Single(f.Store.Runs);
        Assert.NotNull(run.Conversation); Assert.Equal(ConversationRunStage.CleanupPending, run.Stage);
        f.Clock.Advance(60);
        await new DailyConversationScheduler(f.Store, f.Adapters, f.Clock).TickAsync(new(), default);
        Assert.Single(f.Codex.Deleted); Assert.Single(f.Codex.Messages);
    }

    [Fact]
    public async Task CleanupExhaustionIsBoundedAndRestartAllowsRecovery()
    {
        var f = new Fixture(); await f.Scheduler.TickAsync(Enabled, default);
        f.Codex.FailDelete = true; f.Clock.Advance(60);
        for (var i = 0; i < 8; i++) { await f.Scheduler.TickAsync(new(), default); f.Clock.Advance(15); }
        Assert.Equal(3, f.Codex.DeleteCalls);
        f.Codex.FailDelete = false;
        await new DailyConversationScheduler(f.Store, f.Adapters, f.Clock).TickAsync(new(), default);
        Assert.Single(f.Codex.Deleted);
    }

    [Fact]
    public async Task RepeatedDstHourDoesNotDuplicateLocalDate()
    {
        var f = new Fixture(); f.Clock.Zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        f.Clock.SetUtc(DateTimeOffset.Parse("2026-11-01T05:30:00Z"));
        var settings = Enabled with { DailyConversationTime = "01:30" };
        await f.Scheduler.TickAsync(settings, default);
        f.Clock.Advance(3600); await f.Scheduler.TickAsync(settings, default);
        Assert.Single(f.Codex.Messages);
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
        public TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        public override TimeZoneInfo LocalTimeZone => Zone;
        public void Advance(int seconds) { now = now.AddSeconds(seconds); timestamp += Math.Max(0, seconds) * TimeSpan.TicksPerSecond; }
        public void ChangeWallClock(int seconds) => now = now.AddSeconds(seconds);
        public void SetUtc(DateTimeOffset value) => now = value;
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
        public int ReadyCalls, CreatedCalls, DeleteCalls;
        public Action? OnCreate;
        public Func<Task>? OnReadyAsync, OnCreateAsync;
        public Func<CancellationToken, Task>? OnSendAsync;
        public Task EnsureReadyAsync(CancellationToken ct) { ReadyCalls++; return FailReady ? Task.FromException(new IOException()) : OnReadyAsync?.Invoke() ?? Task.CompletedTask; }
        public async Task<CreatedConversation> CreateAndOpenAsync(Guid id, CancellationToken ct)
        {
            CreatedCalls++; OnCreate?.Invoke();
            if (OnCreateAsync is not null) await OnCreateAsync();
            return new CreatedConversation(app, Guid.NewGuid().ToString(), id, DateTimeOffset.UtcNow);
        }
        public Task SendAsync(CreatedConversation c, string text, CancellationToken ct)
        { Messages.Add(text); return FailSend ? Task.FromException(new OperationCanceledException()) : OnSendAsync?.Invoke(ct) ?? Task.CompletedTask; }
        public Task<bool> CanSafelyDeleteAsync(CreatedConversation c, CancellationToken ct) => Task.FromResult(Safe);
        public Task StopAndDeleteAsync(CreatedConversation c, CancellationToken ct)
        { DeleteCalls++; if (FailDelete) throw new IOException(); Deleted.Add(c.ThreadId); return Task.CompletedTask; }
    }
}
