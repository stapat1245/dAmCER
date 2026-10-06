using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DivAcerManagerMax.Services;

public sealed class CustomProfile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "Profile";
    [JsonPropertyName("thermal")] public string Thermal { get; set; } = "balanced";
    [JsonPropertyName("cpu_fan")] public int CpuFan { get; set; }
    [JsonPropertyName("gpu_fan")] public int GpuFan { get; set; }
    [JsonPropertyName("fan_mode")] public string FanMode { get; set; } = "auto"; // auto | max | manual
    [JsonPropertyName("battery_limit")] public bool BatteryLimit { get; set; }
    [JsonPropertyName("usb_charging")] public int UsbCharging { get; set; }
    [JsonPropertyName("created_utc")] public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FanCurvePoint
{
    [JsonPropertyName("temp")] public double Temp { get; set; }
    [JsonPropertyName("percent")] public int Percent { get; set; }
}

public sealed class FanCurveDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "Custom";
    [JsonPropertyName("points")] public List<FanCurvePoint> Points { get; set; } = new();
}

public sealed class AppPreferences
{
    [JsonPropertyName("close_to_tray")] public bool CloseToTray { get; set; }
    [JsonPropertyName("notifications")] public bool Notifications { get; set; } = true;
    [JsonPropertyName("active_curve")] public string? ActiveCurve { get; set; }
}

public sealed class StoreData
{
    [JsonPropertyName("profiles")] public List<CustomProfile> Profiles { get; set; } = new();
    [JsonPropertyName("curves")] public List<FanCurveDefinition> Curves { get; set; } = new();
    [JsonPropertyName("preferences")] public AppPreferences Preferences { get; set; } = new();
}

/// <summary>
///     JSON persistence for custom profiles, fan curves and UI preferences.
///     Stored under the user's config directory (XDG: ~/.config/DivAcerManagerMax).
/// </summary>
public static class ProfileStore
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DivAcerManagerMax");

    private static readonly string StorePath = Path.Combine(ConfigDir, "profiles.json");
    private static readonly object Sync = new();
    private static StoreData? _data;

    private static StoreData Data
    {
        get
        {
            lock (Sync)
            {
                if (_data != null) return _data;
                try
                {
                    if (File.Exists(StorePath))
                    {
                        _data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(StorePath)) ??
                                new StoreData();
                    }
                    else
                    {
                        _data = new StoreData();
                    }
                }
                catch
                {
                    _data = new StoreData();
                }

                // JSON nulls must not leak into the UI.
                _data.Profiles ??= new List<CustomProfile>();
                _data.Curves ??= new List<FanCurveDefinition>();
                _data.Preferences ??= new AppPreferences();

                SeedDefaults(_data);
                return _data;
            }
        }
    }

    public static AppPreferences Preferences => Data.Preferences;

    public static IReadOnlyList<CustomProfile> Profiles => Data.Profiles;

    public static IReadOnlyList<FanCurveDefinition> Curves => Data.Curves;

    public static void Save()
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true });

                // Atomic write: never leave a half-written store behind.
                var tempPath = StorePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, StorePath, true);
            }
            catch
            {
                // Persistence failures must not break the app.
            }
        }
    }

    public static void AddOrUpdateProfile(CustomProfile profile)
    {
        lock (Sync)
        {
            var existing = Data.Profiles.FirstOrDefault(p =>
                p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) Data.Profiles.Remove(existing);
            Data.Profiles.Insert(0, profile);
            Save();
        }
    }

    public static void DeleteProfile(string name)
    {
        lock (Sync)
        {
            var existing = Data.Profiles.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) Data.Profiles.Remove(existing);
            Save();
        }
    }

    public static void SaveCurve(FanCurveDefinition curve)
    {
        lock (Sync)
        {
            var existing = Data.Curves.FirstOrDefault(c =>
                c.Name.Equals(curve.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) Data.Curves.Remove(existing);
            Data.Curves.Add(curve);
            Save();
        }
    }

    public static FanCurveDefinition? GetCurve(string name)
    {
        return Data.Curves.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static void SeedDefaults(StoreData data)
    {
        if (data.Curves.Count == 0)
        {
            data.Curves.Add(new FanCurveDefinition
            {
                Name = "Silent",
                Points = new List<FanCurvePoint>
                {
                    new() { Temp = 40, Percent = 20 },
                    new() { Temp = 55, Percent = 30 },
                    new() { Temp = 70, Percent = 45 },
                    new() { Temp = 85, Percent = 70 },
                    new() { Temp = 95, Percent = 100 }
                }
            });
            data.Curves.Add(new FanCurveDefinition
            {
                Name = "Balanced",
                Points = new List<FanCurvePoint>
                {
                    new() { Temp = 40, Percent = 25 },
                    new() { Temp = 55, Percent = 40 },
                    new() { Temp = 70, Percent = 60 },
                    new() { Temp = 85, Percent = 85 },
                    new() { Temp = 95, Percent = 100 }
                }
            });
            data.Curves.Add(new FanCurveDefinition
            {
                Name = "Aggressive",
                Points = new List<FanCurvePoint>
                {
                    new() { Temp = 40, Percent = 40 },
                    new() { Temp = 55, Percent = 60 },
                    new() { Temp = 70, Percent = 80 },
                    new() { Temp = 85, Percent = 100 },
                    new() { Temp = 95, Percent = 100 }
                }
            });
        }

        if (data.Profiles.Count == 0)
        {
            data.Profiles.Add(new CustomProfile
            {
                Name = "Gaming",
                Thermal = "performance",
                FanMode = "max",
                CpuFan = 100,
                GpuFan = 100
            });
            data.Profiles.Add(new CustomProfile
            {
                Name = "Study",
                Thermal = "quiet",
                FanMode = "auto",
                BatteryLimit = true
            });
        }
    }

    /// <summary>Interpolate a fan percentage for a temperature from a curve.</summary>
    public static int EvaluateCurve(FanCurveDefinition curve, double tempC, int floor = 20)
    {
        var points = curve.Points.OrderBy(p => p.Temp).ToList();
        if (points.Count == 0) return floor;

        if (tempC <= points[0].Temp) return Math.Max(points[0].Percent, floor);
        if (tempC >= points[^1].Temp) return Math.Max(points[^1].Percent, floor);

        for (var i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            if (tempC < a.Temp || tempC > b.Temp) continue;

            var span = b.Temp - a.Temp;
            if (span <= 0) return Math.Max(b.Percent, floor);

            var ratio = (tempC - a.Temp) / span;
            var value = a.Percent + (b.Percent - a.Percent) * ratio;
            return Math.Clamp((int)Math.Round(value), floor, 100);
        }

        return Math.Max(points[^1].Percent, floor);
    }
}
