using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AiUsageWidget.Services;

public sealed class ForegroundProcessReader
{
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
