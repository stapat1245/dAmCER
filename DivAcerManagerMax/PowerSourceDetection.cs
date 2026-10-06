using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Timers;

/// <summary>
///     Polls the AC adapter state and reports changes.
/// </summary>
public sealed class PowerSourceDetection : IDisposable
{
    private readonly List<string> _possiblePowerSupplyPaths;
    private readonly Timer _powerSourceCheckTimer;

    public PowerSourceDetection()
    {
        _possiblePowerSupplyPaths = new List<string>
        {
            "/sys/class/power_supply/AC/online",
            "/sys/class/power_supply/ACAD/online",
            "/sys/class/power_supply/ADP1/online",
            "/sys/class/power_supply/AC0/online"
        };

        _powerSourceCheckTimer = new Timer(5000);
        _powerSourceCheckTimer.Elapsed += OnTimerElapsed;
        _powerSourceCheckTimer.AutoReset = true;
        _powerSourceCheckTimer.Start();

        Publish(IsLaptopPluggedIn());
    }

    public bool IsPluggedIn { get; private set; }

    public void Dispose()
    {
        _powerSourceCheckTimer.Stop();
        _powerSourceCheckTimer.Dispose();
    }

    public event Action<bool>? Changed;

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Publish(IsLaptopPluggedIn());
    }

    private void Publish(bool pluggedIn)
    {
        if (IsPluggedIn == pluggedIn) return;
        IsPluggedIn = pluggedIn;
        Changed?.Invoke(pluggedIn);
    }

    private bool IsLaptopPluggedIn()
    {
        try
        {
            foreach (var path in _possiblePowerSupplyPaths)
                if (File.Exists(path))
                    return File.ReadAllText(path).Trim() == "1";

            return CheckUsingUPower() || CheckUsingLsAcpi();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking power status: {ex.Message}");
            return false;
        }
    }

    private static bool CheckUsingUPower()
    {
        try
        {
            var output = Run("upower", "-i /org/freedesktop/UPower/devices/line_power_AC");
            if (output.Contains("online:") && output.Contains("yes")) return true;
        }
        catch
        {
            // Fall through to acpi.
        }

        return false;
    }

    private static bool CheckUsingLsAcpi()
    {
        try
        {
            return Run("acpi", "-a").Contains("on-line");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking ACPI power status: {ex.Message}");
            return false;
        }
    }

    private static string Run(string command, string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = command;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(2000);
        return output;
    }
}
