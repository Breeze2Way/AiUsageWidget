using System.Windows;
using System.Windows.Threading;
using AiUsageWidget.Models;
using AiUsageWidget.Scheduling;
using AiUsageWidget.Services;

namespace AiUsageWidget;

public partial class MainWindow
{
    private DailyConversationScheduler? dailyScheduler;
    private readonly DispatcherTimer dailyTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly CancellationTokenSource dailyCancellation = new();
    private CodexConversationAdapter? dailyCodex;
    private AntigravityConversationAdapter? dailyAntigravity;
    private bool dailyClosed, dailyTickRunning;

    private void StartDailyScheduling()
    {
        dailyCodex = new(); dailyAntigravity = new();
        dailyScheduler = new(new ConversationRunStore(), [dailyCodex, dailyAntigravity], TimeProvider.System, () => settings);
        dailyScheduler.StatusChanged += (_, _) => { if (!dailyClosed) Dispatcher.BeginInvoke(UpdateDailyStatus); };
        dailyTimer.Tick += async (_, _) => await TickDailyAsync();
        dailyTimer.Start();
        _ = TickDailyAsync();
    }
    private async Task TickDailyAsync()
    {
        if (dailyClosed || dailyScheduler is null) return;
        var snapshot = settings;
        try { await Task.Run(() => dailyScheduler.TickAsync(snapshot, dailyCancellation.Token)); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { App.LogException("DailyConversation", new InvalidOperationException(ex.GetType().Name)); }
        finally { if (!dailyClosed) UpdateDailyStatus(); }
    }
    private void StopDailyScheduling()
    {
        dailyClosed = true; dailyTimer.Stop(); dailyCancellation.Cancel();
        // Pending ownership is already durable; next launch recovers cleanup.
        dailyCodex?.Dispose(); dailyAntigravity?.Dispose();
    }
    private void LoadDailySettings()
    {
        DailyEnabledBox.IsChecked = settings.DailyConversationEnabled;
        DailyTimeBox.Text = settings.DailyConversationTime;
        DailyCodexBox.IsChecked = settings.DailyConversationCodex;
        DailyAntigravityBox.IsChecked = settings.DailyConversationAntigravity;
        UpdateDailyStatus();
    }
    private bool TryReadDailySettings(out WidgetSettings candidate)
    {
        candidate = settings with
        {
            DailyConversationEnabled = DailyEnabledBox.IsChecked == true,
            DailyConversationTime = DailyTimeBox.Text.Trim(),
            DailyConversationCodex = DailyCodexBox.IsChecked == true,
            DailyConversationAntigravity = DailyAntigravityBox.IsChecked == true
        };
        if (DailyConversationOptions.IsValid(candidate)) return true;
        ShowLocalizedMessage("时间必须为 HH:mm（例如 05:30）；启用定时须至少选择一个软件。",
            "Use HH:mm (for example 05:30) and select at least one app when enabled.", "定时设置无效", "Invalid schedule");
        return false;
    }
    private async void TestDailyConversation_Click(object sender, RoutedEventArgs e)
    {
        if (dailyScheduler is null || dailyTickRunning || !TryReadDailySettings(out var candidate)) return;
        var english = WidgetLanguage.IsEnglish(settings.Language);
        if (!candidate.DailyConversationCodex && !candidate.DailyConversationAntigravity)
        {
            ShowLocalizedMessage("请至少选择一个测试软件。", "Select at least one app to test.", "无法测试", "Cannot test"); return;
        }
        if (System.Windows.MessageBox.Show(this, english
            ? "Create a background chat in each selected app, send 今日东京天气, then permanently delete it after 60 seconds? This uses normal quota. Keep the widget running for cleanup."
            : "将在所选软件中后台新建会话，发送“今日东京天气”，60 秒后永久删除。这会消耗正常额度，请保持悬浮球运行以完成清理。是否测试？",
            english ? "Real conversation test" : "真实会话测试", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DailyTestButton.IsEnabled = false; dailyTickRunning = true;
        try { await Task.Run(() => dailyScheduler.TestNowAsync(candidate, dailyCancellation.Token)); }
        catch (OperationCanceledException) { }
        finally { dailyTickRunning = false; if (!dailyClosed) { DailyTestButton.IsEnabled = true; UpdateDailyStatus(); } }
    }
    private void ApplyDailyLanguage(bool english)
    {
        DailySectionText.Text = english ? "Daily temporary chat" : "每日临时会话";
        DailyEnabledBox.Content = english ? "Enable daily schedule" : "启用每日定时";
        DailyTimeLabel.Text = english ? "Local time (HH:mm)" : "本地时间（HH:mm）";
        DailyTestButton.Content = english ? "Test now" : "立即测试";
        DailyHintText.Text = english
            ? "Starts selected apps if needed. Sends 今日东京天气 in a new background chat; deletes it 60s after sending. Widget must be running/PC awake. Missed runs are skipped. Disabling still cleans pending chats. Uses normal quota."
            : "到点自动启动所选软件，后台新建会话发送“今日东京天气”，发送后 60 秒删除。须保持悬浮球运行、电脑唤醒；错过不补发。关闭后仍清理待删除会话，会消耗正常额度。";
        UpdateDailyStatus();
    }
    private void UpdateDailyStatus()
    {
        if (DailyNextText is null || settings is null) return;
        var english = WidgetLanguage.IsEnglish(settings.Language);
        var runs = dailyScheduler?.Runs ?? [];
        var next = DailyConversationStatusFormatter.NextRunLocal(settings, runs, DateTimeOffset.Now);
        DailyNextText.Text = next is { } due ? (english ? "Next (local): " : "下次（本地时间）：") + due.ToString("MM-dd HH:mm")
            : english ? "Schedule disabled" : "定时已关闭";
        if (dailyScheduler?.FaultCode is not null)
        {
            DailyStatusText.Text = english ? "History unavailable/write failed: all automatic actions stopped. Pending chats may need manual cleanup."
                : "执行记录读取/写入异常，已停止自动操作；待清理会话可能需手动处理。";
            foreach (var orphan in dailyScheduler.UnpersistedConversations)
                DailyStatusText.Text += $"\n{orphan.App} ID: {orphan.ThreadId}";
            return;
        }
        DailyStatusText.Text = string.Join("\n", new[] { ConversationApp.Codex, ConversationApp.Antigravity }.Select(app =>
            $"{app} : {DailyConversationStatusFormatter.Result(runs.LastOrDefault(r => r.App == app)?.ResultCode ?? "", english)}"));
    }
}
