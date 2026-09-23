using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AiUsageWidget;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\AiUsageWidget_SingleInstance_Mutex";
    private static Mutex? singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        singleInstanceMutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
            Shutdown();
            return;
        }

        SetupExceptionHandling();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (singleInstanceMutex is not null)
        {
            try
            {
                singleInstanceMutex.ReleaseMutex();
            }
            catch
            {
            }

            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
        }

        base.OnExit(e);
    }

    private void SetupExceptionHandling()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("AppDomainUnhandledException", ex);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    public static void LogException(string category, Exception ex)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AiUsageWidget");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "error.log");
            var entry = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {ex}\n";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
        }
    }
}
