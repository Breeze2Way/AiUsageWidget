using AiUsageWidget.Models;

namespace AiUsageWidget.Scheduling;

public sealed class DailyConversationScheduler
{
    public const string Prompt = "今日东京天气";
    private readonly IConversationRunStore store;
    private readonly Dictionary<ConversationApp, IConversationAdapter> adapters;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim tickGate = new(1, 1);
    private readonly object stateLock = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> exhausted = new();
    private List<ConversationRun> runs = [];
    private string? previousTime;
    private DateTime? suppressedMinute;
    public string? FaultCode { get; private set; }
    public event EventHandler? StatusChanged;
    public IReadOnlyList<ConversationRun> Runs { get { lock (stateLock) return runs.ToArray(); } }

    public DailyConversationScheduler(IConversationRunStore store, IEnumerable<IConversationAdapter> adapters, TimeProvider clock)
    {
        this.store = store; this.adapters = adapters.ToDictionary(a => a.App); this.clock = clock;
        try
        {
            runs = store.Load().ToList();
            ConversationRunStore.Validate(runs);
            var recovered = runs.Select(r => r.Stage switch
            {
                ConversationRunStage.Reserved => r with { Stage = ConversationRunStage.Failed, ResultCode = "interrupted" },
                ConversationRunStage.Created or ConversationRunStage.Sending => r with
                {
                    Stage = ConversationRunStage.CleanupPending, CleanupDueAt = clock.GetUtcNow().AddSeconds(60),
                    ResultCode = "send_uncertain", CleanupAttempts = 0, RetryAt = null
                },
                ConversationRunStage.CleanupPending => r with { CleanupAttempts = 0, RetryAt = null },
                _ => r
            }).ToList();
            if (!runs.SequenceEqual(recovered)) { store.Save(recovered); runs = recovered; }
        }
        catch (Exception) { FaultCode = "history_unavailable"; }
    }

    public Task TickAsync(WidgetSettings settings, CancellationToken ct) => ExecuteAsync(settings, false, ct);
    public Task TestNowAsync(WidgetSettings settings, CancellationToken ct) => ExecuteAsync(settings, true, ct);

    private async Task ExecuteAsync(WidgetSettings settings, bool manual, CancellationToken ct)
    {
        if (!await tickGate.WaitAsync(0, ct).ConfigureAwait(false)) return;
        try
        {
            if (FaultCode is not null) return;
            var local = clock.GetLocalNow();
            var minute = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0);
            if (previousTime is not null && previousTime != settings.DailyConversationTime) suppressedMinute = minute;
            previousTime = settings.DailyConversationTime;
            await Task.WhenAll(adapters.Values.Select(a => CleanupAsync(a, ct))).ConfigureAwait(false);
            if (FaultCode is not null || ct.IsCancellationRequested) return;
            if (!DailyConversationOptions.TryParseTime(settings.DailyConversationTime, out var time)) return;
            if (!manual && (!settings.DailyConversationEnabled || local.Hour != time.Hour || local.Minute != time.Minute || suppressedMinute == minute)) return;
            var date = DateOnly.FromDateTime(local.Date);
            await Task.WhenAll(adapters.Values.Where(a => a.App == ConversationApp.Codex
                    ? settings.DailyConversationCodex : settings.DailyConversationAntigravity)
                .Select(a => StartAsync(a, manual ? null : date, ct))).ConfigureAwait(false);
        }
        finally { tickGate.Release(); StatusChanged?.Invoke(this, EventArgs.Empty); }
    }

    private async Task StartAsync(IConversationAdapter adapter, DateOnly? date, CancellationToken ct)
    {
        ConversationRun run;
        lock (stateLock)
        {
            if (FaultCode is not null || runs.Any(r => r.App == adapter.App &&
                (date is not null && r.ScheduledDate == date || r.Stage is ConversationRunStage.Created or ConversationRunStage.Sending or ConversationRunStage.CleanupPending))) return;
            run = new(Guid.NewGuid(), adapter.App, date);
            if (!Save(run)) return;
        }
        try
        {
            await adapter.EnsureReadyAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            var conversation = await adapter.CreateAndOpenAsync(run.RunId, ct).ConfigureAwait(false);
            run = run with { Conversation = conversation, Stage = ConversationRunStage.Created, ResultCode = "created", CleanupDueAt = clock.GetUtcNow().AddSeconds(60) };
            if (!Save(run)) return; // Do not send without durable exact-ID ownership.
            run = run with { Stage = ConversationRunStage.Sending, SendStartedAt = clock.GetUtcNow(), ResultCode = "sending" };
            if (!Save(run)) return;
            await adapter.SendAsync(conversation, Prompt, ct).ConfigureAwait(false);
            var sent = clock.GetUtcNow();
            Save(run with { Stage = ConversationRunStage.CleanupPending, SentAt = sent, CleanupDueAt = sent.AddSeconds(60), ResultCode = "waiting_cleanup" });
        }
        catch (Exception)
        {
            // Transport errors can mean accepted-but-unacknowledged. Never resend.
            Save(run with
            {
                Stage = run.Conversation is null ? ConversationRunStage.Failed : ConversationRunStage.CleanupPending,
                CleanupDueAt = run.Conversation is null ? null : clock.GetUtcNow().AddSeconds(60),
                ResultCode = run.Conversation is null ? "create_failed" : "send_uncertain"
            });
        }
    }

    private async Task CleanupAsync(IConversationAdapter adapter, CancellationToken ct)
    {
        foreach (var original in Runs.Where(r => r.App == adapter.App && r.Stage == ConversationRunStage.CleanupPending))
        {
            if (FaultCode is not null || ct.IsCancellationRequested || exhausted.ContainsKey(original.RunId) ||
                original.CleanupDueAt is not { } due || clock.GetUtcNow() < due ||
                original.RetryAt is { } retry && clock.GetUtcNow() < retry) continue;
            var run = original;
            try
            {
                ConversationRunStore.Validate([run]);
                await adapter.EnsureReadyAsync(ct).ConfigureAwait(false);
                if (!await adapter.CanSafelyDeleteAsync(run.Conversation!, ct).ConfigureAwait(false))
                {
                    Save(run with { Stage = ConversationRunStage.Preserved, ResultCode = "cleanup_skipped" }); continue;
                }
                // Check again inside the adapter immediately before irreversible deletion.
                await adapter.StopAndDeleteAsync(run.Conversation!, ct).ConfigureAwait(false);
                Save(run with { Stage = ConversationRunStage.Completed, ResultCode = "deleted" });
            }
            catch (Exception)
            {
                var attempts = run.CleanupAttempts + 1;
                if (attempts >= 3) exhausted.TryAdd(run.RunId, 0);
                Save(run with { CleanupAttempts = attempts, RetryAt = clock.GetUtcNow().AddSeconds(15), ResultCode = "cleanup_failed" });
            }
        }
    }

    private bool Save(ConversationRun run)
    {
        lock (stateLock)
        {
            if (FaultCode is not null) return false;
            try
            {
                var copy = runs.Where(r => r.RunId != run.RunId).Append(run).ToList();
                store.Save(copy); runs = copy; return true;
            }
            catch (Exception) { FaultCode = "history_write_failed"; return false; }
        }
    }
}
