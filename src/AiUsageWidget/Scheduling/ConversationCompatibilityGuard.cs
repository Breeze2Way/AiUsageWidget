using System.Diagnostics;

namespace AiUsageWidget.Scheduling;

public sealed class ConversationCompatibilityGuard(
    Func<ConversationApp, CancellationToken, Task<string>> readVersion,
    Func<ConversationApp, CancellationToken, Task> start)
{
    public async Task EnsureReadyAsync(ConversationApp app, CancellationToken ct)
    {
        var version = (await readVersion(app, ct).ConfigureAwait(false)).Trim();
        var verified = app == ConversationApp.Codex ? version is "0.163.0-alpha.10.2" or "0.165.0-alpha.1.2" : version is "2.27.0" or "2.27.0.0";
        if (!verified) throw new NotSupportedException("This app version has not been verified for safe conversation cleanup.");
        await start(app, ct).ConfigureAwait(false);
    }

    public static Task EnsureInstalledReadyAsync(ConversationApp app, CancellationToken ct) =>
        new ConversationCompatibilityGuard(ReadInstalledVersionAsync, DesktopAppLauncher.EnsureStartedAsync).EnsureReadyAsync(app, ct);

    private static async Task<string> ReadInstalledVersionAsync(ConversationApp app, CancellationToken ct)
    {
        if (app == ConversationApp.Antigravity)
            return FileVersionInfo.GetVersionInfo(DesktopAppLauncher.FindAntigravityExe()).ProductVersion ?? "";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var info = new ProcessStartInfo(DesktopAppLauncher.FindCodexCli())
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--version");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot inspect Codex version.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await error.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException("Cannot inspect Codex version.");
            return (await output.ConfigureAwait(false)).Replace("codex-cli ", "", StringComparison.Ordinal).Trim();
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
}
