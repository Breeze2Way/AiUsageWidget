using System.Diagnostics;
using System.IO;

namespace AiUsageWidget.Scheduling;

public static class DesktopAppLauncher
{
    public static string WorkspacePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AiUsageWidget", "daily-workspace");
    public static string FindCodexCli()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        var bundled = Directory.Exists(root) ? Directory.EnumerateFiles(root, "codex.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
        if (bundled is not null) return bundled;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var exe = Path.Combine(dir.Trim('"'), "codex.exe");
            if (File.Exists(exe)) return exe;
        }
        throw new FileNotFoundException("Codex CLI is not installed.");
    }
    public static string FindAntigravityExe()
    {
        foreach (var process in Process.GetProcessesByName("Antigravity"))
            using (process)
            {
                try { if (process.MainModule?.FileName is { } file && File.Exists(file)) return file; }
                catch (System.ComponentModel.Win32Exception) { }
            }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "antigravity", "Antigravity.exe");
    }
    public static async Task EnsureStartedAsync(ConversationApp app, CancellationToken ct)
    {
        static bool Running(ConversationApp value)
        {
            var names = value == ConversationApp.Codex ? new[] { "ChatGPT", "Codex" } : new[] { "Antigravity" };
            foreach (var name in names)
                foreach (var process in Process.GetProcessesByName(name))
                    using (process) { if (!process.HasExited) return true; }
            return false;
        }
        Directory.CreateDirectory(WorkspacePath);
        if (Running(app)) return;
        if (app == ConversationApp.Codex)
            Process.Start(new ProcessStartInfo("codex://") { UseShellExecute = true })?.Dispose();
        else
        {
            var exe = FindAntigravityExe();
            if (!File.Exists(exe)) throw new FileNotFoundException("Antigravity is not installed.");
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })?.Dispose();
        }
        for (var i = 0; i < 60; i++)
        {
            ct.ThrowIfCancellationRequested(); if (Running(app)) return;
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }
        throw new TimeoutException("Desktop app did not start.");
    }
}
