using System.Diagnostics;
using System.Runtime.InteropServices;
using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

public sealed class ForegroundProcessReader
{
    public IReadOnlySet<UsageProvider> ReadRunningProviders()
    {
        var providers = new HashSet<UsageProvider>();
        if (HasRunningProcess("Antigravity"))
        {
            providers.Add(UsageProvider.Antigravity);
        }

        if (HasRunningProcess("Codex") || HasRunningProcess("ChatGPT"))
        {
            providers.Add(UsageProvider.Codex);
        }

        return providers;
    }

    public string? ReadForegroundProcessName()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out var processId) == 0)
            {
                return null;
            }

            using var process = Process.GetProcessById(checked((int)processId));
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasRunningProcess(string processName)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            return false;
        }

        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
