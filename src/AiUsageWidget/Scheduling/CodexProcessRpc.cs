using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AiUsageWidget.Scheduling;

public sealed class CodexProcessRpc : IConversationRpc
{
    private readonly SemaphoreSlim initGate = new(1, 1), writeGate = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private Process? process;
    private int sequence;
    private bool initialized;
    private bool disposed;

    public async Task<JsonElement> CallAsync(string method, object args, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await initGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!initialized)
            {
                if (process is not null) DisposeProcess();
                Directory.CreateDirectory(DesktopAppLauncher.WorkspacePath);
                var info = new ProcessStartInfo(DesktopAppLauncher.FindCodexCli())
                {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = DesktopAppLauncher.WorkspacePath,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                };
                foreach (var arg in new[] { "--no-daemon", "app-server", "--listen", "stdio://" }) info.ArgumentList.Add(arg);
                process = Process.Start(info) ?? throw new IOException("Cannot start Codex service.");
                _ = DrainAsync(process);
                _ = process.StandardError.ReadToEndAsync(); // Drain without logging response bodies/credentials.
                await CallCoreAsync("initialize", new { clientInfo = new { name = "ai_usage_widget", version = "1.0.0" } }, ct).ConfigureAwait(false);
                await WriteAsync(new { method = "initialized", @params = new { } }, ct).ConfigureAwait(false);
                initialized = true;
            }
        }
        finally { initGate.Release(); }
        return await CallCoreAsync(method, args, ct).ConfigureAwait(false);
    }

    private async Task<JsonElement> CallCoreAsync(string method, object args, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref sequence);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var registration = timeout.Token.Register(() => completion.TrySetCanceled(timeout.Token));
        try
        {
            await WriteAsync(new { id, method, @params = args }, timeout.Token).ConfigureAwait(false);
            return await completion.Task.ConfigureAwait(false);
        }
        finally { pending.TryRemove(id, out _); }
    }
    private async Task WriteAsync(object message, CancellationToken ct)
    {
        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var writer = process?.StandardInput ?? throw new IOException("Codex service unavailable.");
            await writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), ct).ConfigureAwait(false);
            await writer.FlushAsync(ct).ConfigureAwait(false);
        }
        finally { writeGate.Release(); }
    }
    private async Task DrainAsync(Process child)
    {
        try
        {
            while (await child.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > 8 * 1024 * 1024) throw new InvalidDataException("Oversized RPC response.");
                using var document = JsonDocument.Parse(line);
                var message = document.RootElement;
                if (!message.TryGetProperty("id", out var value)) continue;
                if (message.TryGetProperty("method", out _))
                {
                    // Never automatically grant a server-initiated approval/tool request.
                    await WriteAsync(new { id = value.Clone(), error = new { code = -32601, message = "Interactive requests are not supported by the daily task." } }, default).ConfigureAwait(false);
                    continue;
                }
                if (!value.TryGetInt32(out var id) || !pending.TryGetValue(id, out var completion)) continue;
                if (message.TryGetProperty("error", out var error))
                    completion.TrySetException(new ConversationRpcException(error.GetProperty("code").ToString(), error.TryGetProperty("message", out var text) ? text.GetString() ?? "" : ""));
                else completion.TrySetResult(message.GetProperty("result").Clone());
            }
        }
        catch (Exception) { }
        finally
        {
            if (ReferenceEquals(child, process))
            {
                initialized = false;
                foreach (var value in pending.Values) value.TrySetException(new IOException("Codex service connection closed."));
            }
        }
    }
    private void DisposeProcess()
    {
        if (process is null) return;
        try { process.StandardInput.Close(); if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        process.Dispose(); process = null;
    }
    public void Dispose() { disposed = true; DisposeProcess(); }
}
