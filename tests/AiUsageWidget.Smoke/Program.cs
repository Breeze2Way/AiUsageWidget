using System.IO;
using System.Text.Json;
using AiUsageWidget.Scheduling;

if (args.Length == 2 && args[0] == "--verify-codex-cleanup")
{
    // Match a Chinese Windows GUI process rather than the UTF-8 dotnet console.
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    Console.OutputEncoding = System.Text.Encoding.GetEncoding(936);
    var run = new ConversationRunStore().Load().Single(r => r.App == ConversationApp.Codex && r.Conversation?.ThreadId == args[1]);
    using var adapter = new CodexConversationAdapter();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    await adapter.EnsureReadyAsync(timeout.Token);
    if (!await adapter.CanSafelyDeleteAsync(run.Conversation!, timeout.Token)) throw new InvalidOperationException("Conversation must be preserved.");
    await adapter.StopAndDeleteAsync(run.Conversation!, timeout.Token);
    Console.WriteLine("Owned conversation cleanup confirmed; no prompt sent.");
    return 0;
}

if (args.Length == 2 && args[0] == "--render-settings")
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            Directory.CreateDirectory(args[1]);
            var window = new AiUsageWidget.MainWindow();
            try
            {
                // Render the real WPF content without showing a native window, starting
                // its Loaded timers, saving settings, or creating any conversation.
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var settingsField = window.GetType().GetField("settings", flags)!;
                var originalSettings = (AiUsageWidget.Models.WidgetSettings)settingsField.GetValue(window)!;
                settingsField.SetValue(window, originalSettings with { Opacity = 0.55 });
                window.Opacity = 0.55;
                window.GetType().GetMethod("ShowSettings", flags)!.Invoke(window, null);
                if (window.Opacity != 1.0) throw new InvalidOperationException("Settings window is not fully opaque.");
                window.GetType().GetMethod("ShowDashboard", flags)!.Invoke(window, null);
                if (Math.Abs(window.Opacity - 0.55) > 0.001) throw new InvalidOperationException("Dashboard opacity was not restored.");
                settingsField.SetValue(window, originalSettings);
                window.GetType().GetMethod("ShowSettings", flags)!.Invoke(window, null);
                ((System.Windows.FrameworkElement)window.FindName("SettingsPanel")).Visibility = System.Windows.Visibility.Visible;
                ((System.Windows.FrameworkElement)window.FindName("DashboardPanel")).Visibility = System.Windows.Visibility.Collapsed;
                var content = (System.Windows.FrameworkElement)window.Content;
                var scroll = (System.Windows.Controls.ScrollViewer)window.FindName("SettingsScroll");
                foreach (var language in new[] { "zh-CN", "en-US" })
                {
                    settingsField.SetValue(window, originalSettings with { Language = language });
                    window.GetType().GetMethod("ApplyLanguage", flags)!.Invoke(window, null);
                    content.Measure(new System.Windows.Size(680, 540));
                    content.Arrange(new System.Windows.Rect(0, 0, 680, 540)); content.UpdateLayout();
                    if (scroll.ScrollableHeight > 0.5)
                        throw new InvalidOperationException($"{language} settings require vertical scrolling: {scroll.ScrollableHeight:0.0} DIPs");
                    foreach (var scale in new[] { 1.0, 1.5 })
                    {
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(680 * scale), (int)(540 * scale), 96 * scale, 96 * scale, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(content);
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var file = File.Create(Path.Combine(args[1], $"settings-{language}-{scale:0.0}.png"));
                        encoder.Save(file);
                    }
                }
            }
            finally
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                (window.GetType().GetField("trayIcon", flags)?.GetValue(window) as IDisposable)?.Dispose();
                (window.GetType().GetField("applicationIcon", flags)?.GetValue(window) as IDisposable)?.Dispose();
            }
        }
        catch (Exception ex) { failure = ex; }
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    if (failure is not null) throw failure;
    Console.WriteLine("Rendered real settings at100%/150%; no timers or model requests started.");
    return 0;
}

// Explicit opt-in integration harness. Never run as part of dotnet test.
if (args.Length == 2 && args[0] == "--live-schedule")
{
    using var scheduledCodex = new CodexConversationAdapter();
    using var scheduledAntig = new AntigravityConversationAdapter();
    var scheduledSettings = new AiUsageWidget.Models.WidgetSettings
    {
        DailyConversationEnabled = true,
        DailyConversationTime = DateTime.Now.AddMinutes(1).ToString("HH:mm")
    };
    var scheduler = new DailyConversationScheduler(new ConversationRunStore(args[1]),
        [scheduledCodex, scheduledAntig], TimeProvider.System, () => scheduledSettings);
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    Console.WriteLine($"SCHEDULED computer local time {scheduledSettings.DailyConversationTime}");
    var previousStatus = "";
    while (!timeout.IsCancellationRequested)
    {
        await scheduler.TickAsync(scheduledSettings, timeout.Token);
        if (scheduler.FaultCode is not null) throw new InvalidOperationException(scheduler.FaultCode);
        var status = string.Join("; ", scheduler.Runs.Select(r => $"{r.App}:{r.Stage}"));
        if (status != previousStatus) { Console.WriteLine(status); previousStatus = status; }
        if (scheduler.Runs.Count == 2 && scheduler.Runs.All(r => r.Stage == ConversationRunStage.CleanupPending))
            scheduledSettings = scheduledSettings with { DailyConversationEnabled = false };
        if (scheduler.Runs.Count == 2 && scheduler.Runs.All(r => r.Stage == ConversationRunStage.Completed))
        {
            await scheduler.TickAsync(scheduledSettings with { DailyConversationEnabled = true }, timeout.Token);
            if (scheduler.Runs.Count != 2) throw new InvalidOperationException("Duplicate scheduled run.");
            Console.WriteLine("PASS: both apps triggered by computer local time; disabling retained cleanup; no duplicate sends.");
            return 0;
        }
        await Task.Delay(1000, timeout.Token);
    }
    throw new TimeoutException("Scheduled smoke did not complete; inspect owned history, never guess-delete.");
}

if (args.Length != 2 || args[0] is not ("--live-create-send-delete" or "--live-codex-candidate"))
{
    Console.Error.WriteLine("Requires --live-create-send-delete <ownership-json-path>; sends real requests and deletes only test-created IDs.");
    return 2;
}
var owned = new List<ConversationRun>();
// Candidate mode bypasses only the version guard for an explicit compatibility test.
using var codex = args[0] == "--live-codex-candidate"
    ? new CodexConversationAdapter(new CodexProcessRpc(), _ => Task.CompletedTask)
    : new CodexConversationAdapter();
using var antig = new AntigravityConversationAdapter();
IConversationAdapter[] selectedAdapters = args[0] == "--live-codex-candidate" ? [codex] : [codex, antig];
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var token = cancellation.Token;
foreach (var adapter in selectedAdapters)
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
foreach (var adapter in selectedAdapters)
{
    var run = owned.Single(r => r.App == adapter.App);
    if (!await adapter.CanSafelyDeleteAsync(run.Conversation!, token)) throw new InvalidOperationException($"{adapter.App}: preserve because ownership/content could not be confirmed.");
    await adapter.StopAndDeleteAsync(run.Conversation!, token);
    owned[owned.IndexOf(run)] = run with { Stage = ConversationRunStage.Completed, ResultCode = "deleted" };
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(owned), token);
    Console.WriteLine($"DELETED {adapter.App}");
}
return 0;
