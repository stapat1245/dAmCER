using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivAcerManagerMax.Services;

public sealed class ServiceState
{
    public string Unit { get; set; } = "";
    public string Label { get; set; } = "";
    public string Active { get; set; } = "unknown";
    public string Enabled { get; set; } = "unknown";
}

/// <summary>
///     Diagnostics helpers: systemd unit state, daemon log tail, Nitro-button stack.
/// </summary>
public static class DiagnosticsService
{
    public const string DaemonLogPath = "/var/log/DAMX_Daemon_Log.log";

    public static async Task<ServiceState> GetServiceStateAsync(string unit, string label)
    {
        var active = await RunAsync("systemctl", $"is-active {unit}");
        var enabled = await RunAsync("systemctl", $"is-enabled {unit}");

        return new ServiceState
        {
            Unit = unit,
            Label = label,
            Active = string.IsNullOrWhiteSpace(active) ? "not found" : active.Trim(),
            Enabled = string.IsNullOrWhiteSpace(enabled) ? "not found" : enabled.Trim()
        };
    }

    public static async Task<ServiceState[]> GetCoreServicesAsync()
    {
        return new[]
        {
            await GetServiceStateAsync("damx-daemon.service", "DAMX daemon"),
            await GetServiceStateAsync("nitro-key-detection.service", "Nitro button detection")
        };
    }

    public static bool IsKeyRemapperRunning()
    {
        return IsProcessRunning("keyd") || IsProcessRunning("kmonad");
    }

    public static bool IsProcessRunning(string name)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories("/proc"))
            {
                var comm = Path.Combine(dir, "comm");
                try
                {
                    if (File.Exists(comm) &&
                        File.ReadAllText(comm).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // Process may have exited.
                }
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public static string TailDaemonLog(int lines = 120)
    {
        try
        {
            if (!File.Exists(DaemonLogPath)) return "Log file not found at " + DaemonLogPath;

            using var stream = new FileStream(DaemonLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var buffer = new System.Collections.Generic.Queue<string>(lines);
            while (reader.ReadLine() is { } line)
            {
                if (buffer.Count == lines) buffer.Dequeue();
                buffer.Enqueue(line);
            }

            return buffer.Count == 0 ? "(log is empty)" : string.Join(Environment.NewLine, buffer);
        }
        catch (UnauthorizedAccessException)
        {
            return "Permission denied reading " + DaemonLogPath +
                   "\nTry: sudo tail -n 100 " + DaemonLogPath;
        }
        catch (Exception ex)
        {
            return $"Could not read log: {ex.Message}";
        }
    }

    public static string BuildReport(string appVersion, DAMXSettings? settings, SystemSnapshot snap,
        ServiceState[] services)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DAMX diagnostics report");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("== Application ==");
        sb.AppendLine($"GUI version      : {appVersion}");
        sb.AppendLine($"Daemon version   : {settings?.Version ?? "unknown"}");
        sb.AppendLine($"Driver version   : {settings?.DriverVersion ?? "unknown"}");
        sb.AppendLine();
        sb.AppendLine("== Hardware ==");
        sb.AppendLine($"Laptop model     : {snap.Model}");
        sb.AppendLine($"Laptop type      : {settings?.LaptopType ?? "unknown"}");
        sb.AppendLine($"CPU              : {snap.CpuName}");
        sb.AppendLine($"GPU              : {snap.GpuName} ({snap.GpuVendor})");
        sb.AppendLine($"Kernel           : {snap.KernelVersion}");
        sb.AppendLine($"OS               : {snap.OsVersion}");
        sb.AppendLine();
        sb.AppendLine("== Sensors ==");
        sb.AppendLine($"CPU temperature  : {snap.CpuTempSource}");
        sb.AppendLine($"GPU metrics      : {snap.GpuSource}");
        sb.AppendLine($"Fans             : {snap.FanSource}");
        sb.AppendLine($"Battery          : {snap.BatterySource}");
        sb.AppendLine();
        sb.AppendLine("== Services ==");
        foreach (var service in services)
            sb.AppendLine($"{service.Label,-24}: {service.Active} ({service.Enabled})");
        sb.AppendLine($"Key remapper     : {(IsKeyRemapperRunning() ? "running (Nitro button grabbed)" : "not running")}");
        sb.AppendLine();
        sb.AppendLine("== Features ==");
        sb.AppendLine(settings?.AvailableFeatures is { Count: > 0 }
            ? string.Join(", ", settings.AvailableFeatures)
            : "(none reported)");
        return sb.ToString();
    }

    private static async Task<string> RunAsync(string command, string arguments)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(info);
            if (process == null) return "";

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var exitTask = process.WaitForExitAsync();
            if (await Task.WhenAny(exitTask, Task.Delay(2500)) != exitTask)
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // ignored
                }
            }

            await Task.WhenAny(Task.WhenAll(outputTask, errorTask), Task.Delay(500));
            return outputTask.IsCompletedSuccessfully ? outputTask.Result : "";
        }
        catch
        {
            return "";
        }
    }
}
