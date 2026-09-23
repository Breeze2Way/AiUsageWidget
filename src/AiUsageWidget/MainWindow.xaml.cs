using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using AiUsageWidget.Controls;
using AiUsageWidget.Data;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using CodexWidgetViewState = CodexUsageWidget.Models.WidgetViewState;

namespace AiUsageWidget;

public partial class MainWindow : Window
{
    private const double BallWindowSize = 68;
    private const double WaterBallSize = 62;
    internal const double SettingsWindowWidth = 500;
    internal const double SettingsWindowHeight = 560;
    private const double SettingsWindowGap = 12;
    private const string CodexUsageUrl = "https://chatgpt.com";
    private const string StartupValueName = "AiUsageWidget";

    private readonly SettingsStore settingsStore = new();
    private readonly WaterBallControl waterBall = new();
    private readonly System.Windows.Controls.TextBlock ballDetailsText = CreateDetailsTextBlock();
    private readonly System.Windows.Controls.TextBlock hostDetailsText = CreateDetailsTextBlock();
    private readonly AntigravityUsageRefreshService refreshService;
    private readonly CodexUsageProvider codexUsageProvider;
    private readonly ForegroundProcessReader foregroundProcessReader = new();
    private readonly DispatcherTimer refreshTimer;
    private readonly DispatcherTimer providerSelectionTimer;
    private readonly DispatcherTimer resetCountdownTimer;
    private readonly DispatcherTimer officialRetryTimer;
    private readonly UserActivityMonitor userActivityMonitor = new();
    private Forms.NotifyIcon trayIcon = null!;
    private Forms.ToolStripMenuItem traySettingsMenuItem = null!;
    private Forms.ToolStripMenuItem trayOfficialUsageMenuItem = null!;
    private Forms.ToolStripMenuItem trayCodexUsageMenuItem = null!;
    private Forms.ToolStripMenuItem trayLanguageMenuItem = null!;
    private Forms.ToolStripMenuItem trayExitMenuItem = null!;
    private System.Drawing.Icon? applicationIcon;
    private WidgetSettings settings;
    private UsageProvider activeProvider = UsageProvider.Antigravity;
    private bool isRefreshing;
    private bool localRefreshPending;
    private WidgetViewState? lastState;
    private CodexWidgetViewState? lastCodexState;
    private string? lastDetails;
    private System.Windows.Point? dashboardPosition;
    private DateTimeOffset? lastOfficialReadAt;
    private bool isExplicitExit;
    private uint taskbarCreatedMessage;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Icon = LoadWindowIcon();
        waterBall.Width = WaterBallSize;
        waterBall.Height = WaterBallSize;
        waterBall.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
        waterBall.VerticalAlignment = VerticalAlignment.Center;
        waterBall.SnapsToDevicePixels = true;
        AutomationProperties.SetName(waterBall, "AI 用量剩余百分比");
        ToolTipService.SetInitialShowDelay(waterBall, 150);
        ToolTipService.SetShowDuration(waterBall, 60000);
        ToolTipService.SetBetweenShowDelay(waterBall, 100);
        waterBall.ToolTip = ballDetailsText;
        WaterBallHost.ToolTip = hostDetailsText;
        WaterBallHost.Children.Add(waterBall);
        settings = settingsStore.Load();
        ApplyWeeklyRingSettings();
        refreshService = CreateRefreshService();
        codexUsageProvider = new CodexUsageProvider();
        refreshTimer = new DispatcherTimer();
        refreshTimer.Tick += RefreshTimer_Tick;
        providerSelectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        providerSelectionTimer.Tick += ProviderSelectionTimer_Tick;
        resetCountdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        resetCountdownTimer.Tick += ResetCountdownTimer_Tick;
        officialRetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        officialRetryTimer.Tick += OfficialRetryTimer_Tick;
        ConfigureWindow();
        ConfigureTrayIcon();
        ApplyLanguage();
    }

    internal static AntigravityUsageRefreshService CreateRefreshService(
        Func<AntigravityQuotaSnapshot?>? readOfficialUsage = null,
        Func<DateTimeOffset, AntigravityTokenUsageSummary>? readTokenUsage = null)
    {
        var reader = new AntigravityStatusReader();
        var tokenReader = new AntigravityTokenUsageReader();
        return new AntigravityUsageRefreshService(
            readOfficialUsage ?? reader.ReadUsage,
            readTokenUsage ?? tokenReader.Read);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        SelectActiveProvider();
        providerSelectionTimer.Start();
        ConfigureRefreshTimer();
        resetCountdownTimer.Start();
        RefreshAsync(refreshOfficial: false);
        officialRetryTimer.Start();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }

        taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_MINIMIZE)
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (taskbarCreatedMessage != 0 && msg == taskbarCreatedMessage)
        {
            if (trayIcon is not null)
            {
                trayIcon.Visible = false;
                trayIcon.Visible = true;
            }
        }

        return IntPtr.Zero;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!isExplicitExit)
        {
            e.Cancel = true;
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                ShowDashboard();
            }
        }
    }

    private void ExitApplication()
    {
        isExplicitExit = true;
        Close();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        refreshTimer.Stop();
        providerSelectionTimer.Stop();
        resetCountdownTimer.Stop();
        officialRetryTimer.Stop();
        var savedPosition = dashboardPosition ?? new System.Windows.Point(Left, Top);
        settingsStore.Save(settings with { Left = savedPosition.X, Top = savedPosition.Y });
        trayIcon.Visible = false;
        trayIcon.Dispose();
        applicationIcon?.Dispose();
    }

    private void ConfigureWindow()
    {
        Topmost = settings.Topmost;
        Opacity = settings.Opacity;
    }

    private void RestoreWindow()
    {
        WindowState = WindowRestorePolicy.GetRestoredState(WindowState);
        if (!IsVisible)
        {
            Show();
        }

        Activate();
    }

    private void PositionWindow()
    {
        var windowSize = new System.Windows.Size(BallWindowSize, BallWindowSize);
        if (double.IsFinite(settings.Left) && double.IsFinite(settings.Top))
        {
            var targetPoint = new System.Windows.Point(settings.Left, settings.Top);
            var workArea = GetWorkAreaForPosition(targetPoint);
            var savedPosition = WindowPlacementCalculator.ClampInsideWorkArea(
                targetPoint,
                windowSize,
                workArea);
            Left = savedPosition.X;
            Top = savedPosition.Y;
            return;
        }

        var defaultWorkArea = GetCurrentWorkArea();
        var defaultPosition = WindowPlacementCalculator.ClampInsideWorkArea(
            new System.Windows.Point(defaultWorkArea.Right - BallWindowSize - 24, defaultWorkArea.Top + 24),
            windowSize,
            defaultWorkArea);
        Left = defaultPosition.X;
        Top = defaultPosition.Y;
    }

    private void ConfigureRefreshTimer()
    {
        refreshTimer.Interval = TimeSpan.FromSeconds(settings.RefreshSeconds);
        refreshTimer.Start();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (userActivityMonitor.IsUserActive())
        {
            officialRetryTimer.Start();
            return;
        }

        if (OfficialRefreshPolicy.ShouldReadAutomatically(
                userActivityMonitor.IsUserActive(),
                DateTimeOffset.UtcNow,
                lastOfficialReadAt))
        {
            RefreshAsync(refreshOfficial: true);
        }
    }

    private void ProviderSelectionTimer_Tick(object? sender, EventArgs e)
    {
        var previousProvider = activeProvider;
        SelectActiveProvider();
        if (previousProvider != activeProvider)
        {
            ApplyActiveProvider();
            if (lastDetails is not null)
            {
                SetDetails(lastDetails);
            }
        }
    }

    private void SelectActiveProvider()
    {
        activeProvider = UsageProviderSelector.Select(
            foregroundProcessReader.ReadForegroundProcessName(),
            activeProvider);
    }

    private void OfficialRetryTimer_Tick(object? sender, EventArgs e)
    {
        if (userActivityMonitor.IsUserActive())
        {
            return;
        }

        if (!OfficialRefreshPolicy.ShouldReadAutomatically(
                userActive: false,
                now: DateTimeOffset.UtcNow,
                lastReadAt: lastOfficialReadAt))
        {
            officialRetryTimer.Stop();
            return;
        }

        officialRetryTimer.Stop();
        RefreshAsync(refreshOfficial: true);
    }

    private async void RefreshAsync(bool refreshOfficial = false)
    {
        if (isRefreshing)
        {
            if (!refreshOfficial)
            {
                localRefreshPending = true;
            }

            return;
        }

        isRefreshing = true;
        if (refreshOfficial)
        {
            lastOfficialReadAt = DateTimeOffset.UtcNow;
        }

        try
        {
            var snapshots = await Task.Run(() =>
            {
                WidgetViewState? antigravity = null;
                CodexWidgetViewState? codex = null;
                var now = DateTimeOffset.UtcNow;
                try
                {
                    antigravity = refreshService.Refresh(now, refreshOfficial);
                }
                catch
                {
                    // A failure in one provider must not suppress the other provider's update.
                }

                try
                {
                    codex = codexUsageProvider.Refresh(now, settings, refreshOfficial);
                }
                catch
                {
                    // Keep the last Codex state if its local files or session are unavailable.
                }

                return (Antigravity: antigravity, Codex: codex);
            });

            lastState = snapshots.Antigravity ?? lastState;
            lastCodexState = snapshots.Codex ?? lastCodexState;
            ApplyCombinedSnapshot();
        }
        catch (Exception exception)
        {
            SetDetails(WidgetLanguage.IsEnglish(settings.Language)
                ? $"Refresh failed: {exception.Message}"
                : $"刷新失败：{exception.Message}");
        }
        finally
        {
            isRefreshing = false;
            if (localRefreshPending)
            {
                localRefreshPending = false;
                RefreshAsync(refreshOfficial: false);
            }
        }
    }

    private void ApplyCombinedSnapshot()
    {
        ApplyActiveProvider();
        SetDetails(AiUsageTooltipFormatter.FormatDetails(
            lastState,
            lastCodexState,
            GetLatestRefreshTime(),
            english: WidgetLanguage.IsEnglish(settings.Language)));
    }

    private DateTimeOffset GetLatestRefreshTime()
    {
        if (lastState is null)
        {
            return lastCodexState?.RefreshedAt ?? DateTimeOffset.Now;
        }

        if (lastCodexState is null || lastState.RefreshedAt >= lastCodexState.RefreshedAt)
        {
            return lastState.RefreshedAt;
        }

        return lastCodexState.RefreshedAt;
    }

    private void ApplyActiveProvider()
    {
        double? fiveHourPercent;
        double? weeklyPercent;
        if (activeProvider == UsageProvider.Codex)
        {
            fiveHourPercent = lastCodexState?.FiveHourRemainingPercent;
            weeklyPercent = lastCodexState?.OfficialRemainingPercent;
        }
        else
        {
            fiveHourPercent = lastState?.FiveHourRemainingPercent;
            weeklyPercent = lastState?.OfficialRemainingPercent;
        }

        var centerPercent = fiveHourPercent ?? weeklyPercent;
        var centerText = WaterBallDisplay.FormatCenterText(centerPercent);
        waterBall.FiveHourRemainingPercent = fiveHourPercent;
        waterBall.WeeklyRemainingPercent = weeklyPercent;
        waterBall.RemainingPercent = centerPercent;
        waterBall.CenterText = centerText;
    }

    private void SetDetails(string details)
    {
        lastDetails = details;
        ApplyTooltipDetails(
            details,
            AiUsageTooltipFormatter.FormatResetDetails(
                lastState,
                lastCodexState,
                DateTimeOffset.Now,
                english: WidgetLanguage.IsEnglish(settings.Language)));
    }

    private void ResetCountdownTimer_Tick(object? sender, EventArgs e)
    {
        if (lastDetails is not null)
        {
            ApplyTooltipDetails(
                lastDetails,
                AiUsageTooltipFormatter.FormatResetDetails(
                    lastState,
                    lastCodexState,
                    DateTimeOffset.Now,
                    english: WidgetLanguage.IsEnglish(settings.Language)));
        }
    }

    private void ApplyTooltipDetails(string details, string? resetDetails)
    {
        foreach (var textBlock in new[] { ballDetailsText, hostDetailsText })
        {
            textBlock.Inlines.Clear();
            foreach (var inline in CreateTooltipInlines(details, resetDetails, activeProvider))
            {
                textBlock.Inlines.Add(inline);
            }
        }
    }

    internal static IReadOnlyList<Inline> CreateTooltipInlines(
        string details,
        string? resetDetails,
        UsageProvider activeProvider)
    {
        var inlines = new List<Inline>();
        var lines = AiUsageTooltipPresentation.Build(details, resetDetails, activeProvider);
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                inlines.Add(new LineBreak());
            }

            var line = lines[index];
            inlines.Add(new Run(line.Text)
            {
                Foreground = line.IsHighlighted
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80))
                    : System.Windows.Media.Brushes.White,
                FontWeight = line.IsHighlighted
                    ? System.Windows.FontWeights.SemiBold
                    : System.Windows.FontWeights.Normal
            });
        }

        return inlines;
    }

    internal static System.Windows.Controls.TextBlock CreateDetailsTextBlock()
    {
        return new System.Windows.Controls.TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 600,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Margin = new Thickness(0)
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The window can close while a drag starts.
            }
        }
    }

    private void Ball_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var startPosition = new System.Windows.Point(Left, Top);
        Header_MouseLeftButtonDown(sender, e);
        if (BallInteractionPolicy.ShouldRefreshAfterDrag(
                Left - startPosition.X,
                Top - startPosition.Y))
        {
            RefreshAsync(OfficialRefreshPolicy.ShouldReadOnManualRefresh);
        }
    }

    private void SettingsHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Header_MouseLeftButtonDown(sender, e);
    }

    private System.Windows.Rect GetWorkAreaForPosition(System.Windows.Point targetPoint)
    {
        var workAreas = GetAllWorkAreas();
        return WindowPlacementCalculator.FindBestWorkArea(
            targetPoint,
            workAreas,
            GetCurrentWorkArea());
    }

    private List<System.Windows.Rect> GetAllWorkAreas()
    {
        var list = new List<System.Windows.Rect>();
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice;

        foreach (var screen in Forms.Screen.AllScreens)
        {
            var wa = screen.WorkingArea;
            if (transform.HasValue)
            {
                var topLeft = transform.Value.Transform(new System.Windows.Point(wa.Left, wa.Top));
                var bottomRight = transform.Value.Transform(new System.Windows.Point(wa.Right, wa.Bottom));
                list.Add(new System.Windows.Rect(topLeft, bottomRight));
            }
            else
            {
                list.Add(new System.Windows.Rect(wa.Left, wa.Top, wa.Width, wa.Height));
            }
        }

        return list;
    }

    private System.Windows.Rect GetCurrentWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return SystemParameters.WorkArea;
        }

        var workArea = Forms.Screen.FromHandle(handle).WorkingArea;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return new System.Windows.Rect(
                workArea.Left,
                workArea.Top,
                workArea.Width,
                workArea.Height);
        }

        var transform = source.CompositionTarget.TransformFromDevice;
        var topLeft = transform.Transform(new System.Windows.Point(workArea.Left, workArea.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(workArea.Right, workArea.Bottom));
        return new System.Windows.Rect(topLeft, bottomRight);
    }

    private void ConfigureTrayIcon()
    {
        try
        {
            var iconPath = AppIcon.GetPath(AppContext.BaseDirectory);
            if (File.Exists(iconPath))
            {
                applicationIcon = new System.Drawing.Icon(iconPath);
            }
        }
        catch (Exception ex)
        {
            App.LogException("ConfigureTrayIcon", ex);
            applicationIcon = null;
        }

        trayIcon = new Forms.NotifyIcon
        {
            Icon = applicationIcon ?? System.Drawing.SystemIcons.Application,
            Text = "AI 用量悬浮球",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip
        {
            AutoSize = true,
            Padding = new Forms.Padding(4),
            ShowCheckMargin = false,
            ShowImageMargin = false,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F)
        };
        traySettingsMenuItem = AddTrayMenuItem(menu, "设置", (_, _) => Dispatcher.Invoke(ShowSettings));
        trayOfficialUsageMenuItem = AddTrayMenuItem(menu, "打开 Antigravity", (_, _) => Dispatcher.Invoke(OpenOfficialUsage));
        trayCodexUsageMenuItem = AddTrayMenuItem(menu, "打开 Codex 用量", (_, _) => Dispatcher.Invoke(OpenCodexUsage));
        trayLanguageMenuItem = AddTrayMenuItem(menu, "English", (_, _) => Dispatcher.Invoke(ToggleLanguage));
        trayExitMenuItem = AddTrayMenuItem(menu, "退出", (_, _) => Dispatcher.Invoke(ExitApplication));
        trayIcon.ContextMenuStrip = menu;
        trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ShowDashboard);
            }
        };
    }

    private static BitmapImage? LoadWindowIcon()
    {
        try
        {
            var iconPath = AppIcon.GetPath(AppContext.BaseDirectory);
            if (!File.Exists(iconPath))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(iconPath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            App.LogException("LoadWindowIcon", ex);
            return null;
        }
    }

    private static Forms.ToolStripMenuItem AddTrayMenuItem(
        Forms.ContextMenuStrip menu,
        string text,
        EventHandler click)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            Padding = new Forms.Padding(8, 4, 8, 4)
        };
        item.Click += click;
        menu.Items.Add(item);
        return item;
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    private void ShowSettings()
    {
        RestoreWindow();
        if (SettingsPanel.Visibility != Visibility.Visible)
        {
            dashboardPosition = new System.Windows.Point(Left, Top);
        }

        var anchorBounds = new System.Windows.Rect(
            dashboardPosition?.X ?? Left,
            dashboardPosition?.Y ?? Top,
            BallWindowSize,
            BallWindowSize);
        var settingsPosition = WindowPlacementCalculator.CalculateSettingsPosition(
            anchorBounds,
            new System.Windows.Size(SettingsWindowWidth, SettingsWindowHeight),
            GetCurrentWorkArea(),
            SettingsWindowGap);

        Width = SettingsWindowWidth;
        Height = SettingsWindowHeight;
        Left = settingsPosition.X;
        Top = settingsPosition.Y;
        WeeklyBudgetBox.Text = settings.WeeklyBudgetConfigured
            ? settings.WeeklyBudgetTokens.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        RefreshSecondsBox.Text = settings.RefreshSeconds.ToString(CultureInfo.InvariantCulture);
        OpacitySlider.Value = settings.Opacity * 100;
        TopmostBox.IsChecked = settings.Topmost;
        AutoStartBox.IsChecked = settings.AutoStart;
        WeeklyRingModeBox.SelectedIndex = settings.WeeklyRingGradientEnabled ? 1 : 0;
        WeeklyRingStartColorBox.Text = settings.WeeklyRingColor;
        WeeklyRingEndColorBox.Text = settings.WeeklyRingGradientColor;
        WeeklyRingTrackColorBox.Text = settings.WeeklyRingTrackColor;
        UpdateRingColorInputs();
        DashboardPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Visible;
    }

    private void ShowDashboard()
    {
        RestoreWindow();
        SettingsPanel.Visibility = Visibility.Collapsed;
        DashboardPanel.Visibility = Visibility.Visible;
        Width = BallWindowSize;
        Height = BallWindowSize;
        if (dashboardPosition is { } position)
        {
            Left = position.X;
            Top = position.Y;
            dashboardPosition = null;
        }
    }

    private void SettingsCancel_Click(object sender, RoutedEventArgs e) => ShowDashboard();

    private void SettingsSave_Click(object sender, RoutedEventArgs e)
    {
        var hasWeeklyBudget = !string.IsNullOrWhiteSpace(WeeklyBudgetBox.Text);
        var weeklyBudget = 0L;
        if ((hasWeeklyBudget &&
                (!long.TryParse(WeeklyBudgetBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out weeklyBudget) || weeklyBudget <= 0)) ||
            !int.TryParse(RefreshSecondsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var refreshSeconds))
        {
            ShowLocalizedMessage(
                "Codex 周预算和刷新间隔必须是有效数字。",
                "Codex weekly budget and refresh interval must be valid numbers.",
                "设置无效",
                "Invalid settings");
            return;
        }

        if (!ColorParser.TryParseHex(WeeklyRingStartColorBox.Text, out var startColor) ||
            !ColorParser.TryParseHex(WeeklyRingEndColorBox.Text, out var endColor) ||
            !ColorParser.TryParseHex(WeeklyRingTrackColorBox.Text, out var trackColor))
        {
            ShowLocalizedMessage(
                "外圈颜色和底色必须是有效的十六进制颜色，例如 #58B7E8。",
                "Ring colors and track color must be valid hexadecimal colors, for example #58B7E8.",
                "设置无效",
                "Invalid settings");
            return;
        }

        var newSettings = SettingsStore.Normalize(settings with
        {
            WeeklyBudgetTokens = weeklyBudget,
            WeeklyBudgetConfigured = hasWeeklyBudget,
            RefreshSeconds = refreshSeconds,
            Opacity = OpacitySlider.Value / 100,
            Topmost = TopmostBox.IsChecked == true,
            AutoStart = AutoStartBox.IsChecked == true,
            WeeklyRingColor = ColorParser.ToHex(startColor),
            WeeklyRingGradientColor = ColorParser.ToHex(endColor),
            WeeklyRingTrackColor = ColorParser.ToHex(trackColor),
            WeeklyRingGradientEnabled = WeeklyRingModeBox.SelectedIndex == 1
        });

        settingsStore.Save(newSettings);
        settings = newSettings;
        ApplyWeeklyRingSettings();
        Topmost = settings.Topmost;
        Opacity = settings.Opacity;
        ConfigureRefreshTimer();
        SetAutoStart(settings.AutoStart);
        ShowDashboard();
        RefreshAsync(refreshOfficial: false);
    }

    private void WeeklyRingMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRingColorInputs();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText is not null)
        {
            OpacityValueText.Text = SettingsDisplayFormatter.FormatOpacityPercent(e.NewValue);
        }
    }

    private void RingColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRingColorInputs();
    }

    private void PickStartColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        PickRingColor(WeeklyRingStartColorBox);
        e.Handled = true;
    }

    private void PickEndColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        PickRingColor(WeeklyRingEndColorBox);
        e.Handled = true;
    }

    private void PickTrackColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        PickRingColor(WeeklyRingTrackColorBox);
        e.Handled = true;
    }

    private void PickRingColor(System.Windows.Controls.TextBox textBox)
    {
        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            FullOpen = true,
            SolidColorOnly = true
        };
        if (ColorParser.TryParseHex(textBox.Text, out var currentColor))
        {
            dialog.Color = System.Drawing.Color.FromArgb(
                currentColor.Red,
                currentColor.Green,
                currentColor.Blue);
        }

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            textBox.Text = ColorParser.ToHex(ColorParser.FromDrawingColor(dialog.Color));
        }
    }

    private void UpdateRingColorInputs()
    {
        if (WeeklyRingEndColorBox is null)
        {
            return;
        }

        WeeklyRingEndColorBox.IsEnabled = WeeklyRingModeBox.SelectedIndex == 1;
        UpdateColorPreview(WeeklyRingStartColorBox, WeeklyRingStartPreview);
        UpdateColorPreview(WeeklyRingEndColorBox, WeeklyRingEndPreview);
        UpdateColorPreview(WeeklyRingTrackColorBox, WeeklyRingTrackPreview);
    }

    private static void UpdateColorPreview(System.Windows.Controls.TextBox textBox, Border preview)
    {
        preview.Background = ColorParser.TryParseHex(textBox.Text, out var color)
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue))
            : System.Windows.Media.Brushes.Transparent;
        preview.BorderBrush = ColorParser.TryParseHex(textBox.Text, out _)
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225))
            : System.Windows.Media.Brushes.IndianRed;
    }

    private void ApplyWeeklyRingSettings()
    {
        var startColor = ColorParser.TryParseHex(settings.WeeklyRingColor, out var parsedStart)
            ? parsedStart
            : ColorParser.DefaultWeeklyRingStartColor;
        var endColor = ColorParser.TryParseHex(settings.WeeklyRingGradientColor, out var parsedEnd)
            ? parsedEnd
            : ColorParser.DefaultWeeklyRingEndColor;
        var trackColor = ColorParser.TryParseHex(settings.WeeklyRingTrackColor, out var parsedTrack)
            ? parsedTrack
            : ColorParser.DefaultWeeklyRingTrackColor;
        waterBall.WeeklyRingStartColor = startColor;
        waterBall.WeeklyRingEndColor = endColor;
        waterBall.WeeklyRingTrackColor = trackColor;
        waterBall.WeeklyRingGradientEnabled = settings.WeeklyRingGradientEnabled;
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        ToggleLanguage();
    }

    private void ToggleLanguage()
    {
        settings = SettingsStore.Normalize(settings with
        {
            Language = WidgetLanguage.IsEnglish(settings.Language)
                ? WidgetLanguage.Chinese
                : WidgetLanguage.English
        });
        settingsStore.Save(settings);
        ApplyLanguage();
        ApplyCombinedSnapshot();
    }

    private void ApplyLanguage()
    {
        var isEnglish = WidgetLanguage.IsEnglish(settings.Language);
        Title = isEnglish ? "AI Usage Widget" : "AI 用量悬浮球";

        SettingsMenuItem.Header = isEnglish ? "Settings" : "设置";
        OfficialUsageMenuItem.Header = isEnglish ? "Open Antigravity" : "打开 Antigravity";
        CodexUsageMenuItem.Header = isEnglish ? "Open Codex usage" : "打开 Codex 用量";
        LanguageMenuItem.Header = isEnglish ? "切换到中文" : "Switch to English";
        ExitMenuItem.Header = isEnglish ? "Exit" : "退出";

        SettingsTitleText.Text = isEnglish ? "Widget settings" : "小工具设置";
        SettingsSubtitleText.Text = isEnglish
            ? "Adjust refresh and ring appearance"
            : "调整数据刷新与外圈显示样式";
        DataRefreshSectionText.Text = isEnglish ? "Data and refresh" : "数据与刷新";
        WeeklyBudgetLabel.Text = isEnglish ? "Codex weekly budget (tokens)" : "Codex 周预算（token）";
        RefreshIntervalLabel.Text = isEnglish ? "Refresh interval (seconds)" : "刷新间隔（秒）";
        OpacityLabel.Text = isEnglish ? "Window opacity" : "窗口不透明度";
        RingStyleSectionText.Text = isEnglish ? "Ring style" : "外圈样式";
        RingModeLabel.Text = isEnglish ? "Color mode" : "颜色模式";
        SolidModeItem.Content = isEnglish ? "Solid" : "纯色";
        GradientModeItem.Content = isEnglish ? "Gradient" : "渐变色";
        StartColorLabel.Text = isEnglish ? "Start color" : "起始颜色";
        EndColorLabel.Text = isEnglish ? "End color" : "结束颜色";
        TrackColorLabel.Text = isEnglish ? "Track color" : "底色";
        ColorHintText.Text = isEnglish
            ? "Use #RRGGBB or click a color swatch. Solid mode uses the start color."
            : "支持 #RRGGBB，也可以点击色块选择颜色；纯色模式只使用起始颜色。";
        var colorPickerTip = isEnglish ? "Click to choose a color" : "点击色块选择颜色";
        WeeklyRingStartPreview.ToolTip = colorPickerTip;
        WeeklyRingEndPreview.ToolTip = colorPickerTip;
        WeeklyRingTrackPreview.ToolTip = colorPickerTip;
        TopmostBox.Content = isEnglish ? "Always on top" : "窗口置顶";
        AutoStartBox.Content = isEnglish ? "Start with Windows" : "开机启动";
        CancelButton.Content = isEnglish ? "Cancel" : "取消";
        SaveButton.Content = isEnglish ? "Save settings" : "保存设置";
        WeeklyBudgetBox.ToolTip = isEnglish
            ? "Optional budget used for Codex local usage estimates; leave blank to disable."
            : "用于 Codex 本地用量估算的可选周预算；留空可关闭。";
        RefreshSecondsBox.ToolTip = isEnglish
            ? "Official usage query interval, from 10 to 600 seconds."
            : "官方用量查询间隔，范围 10–600 秒";

        if (traySettingsMenuItem is not null)
        {
            traySettingsMenuItem.Text = isEnglish ? "Settings" : "设置";
            trayOfficialUsageMenuItem.Text = isEnglish ? "Open Antigravity" : "打开 Antigravity";
            trayCodexUsageMenuItem.Text = isEnglish ? "Open Codex usage" : "打开 Codex 用量";
            trayLanguageMenuItem.Text = isEnglish ? "切换到中文" : "Switch to English";
            trayExitMenuItem.Text = isEnglish ? "Exit" : "退出";
            trayIcon.Text = isEnglish ? "AI Usage Widget" : "AI 用量悬浮球";
        }
    }

    private void ShowLocalizedMessage(
        string chineseMessage,
        string englishMessage,
        string chineseTitle,
        string englishTitle)
    {
        var isEnglish = WidgetLanguage.IsEnglish(settings.Language);
        System.Windows.MessageBox.Show(
            isEnglish ? englishMessage : chineseMessage,
            isEnglish ? englishTitle : chineseTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled && !string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                key.SetValue(StartupValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            App.LogException("SetAutoStart", ex);
        }
    }

    private static void OpenOfficialUsage()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("Antigravity"))
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(process.MainWindowHandle, 9);
                        SetForegroundWindow(process.MainWindowHandle);
                        return;
                    }
                }
                catch
                {
                    // Continue with the installed executable fallback.
                }
                finally
                {
                    process.Dispose();
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "antigravity.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            App.LogException("OpenOfficialUsage", ex);
        }
    }

    private static void OpenCodexUsage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = CodexUsageUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            App.LogException("OpenCodexUsage", ex);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    private void OfficialUsage_Click(object sender, RoutedEventArgs e) => OpenOfficialUsage();

    private void CodexUsage_Click(object sender, RoutedEventArgs e) => OpenCodexUsage();

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();
}
