using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DivAcerManagerMax.Services;

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

/// <summary>
///     Point-in-time reading of every sensor the GUI cares about.
/// </summary>
public sealed class SystemSnapshot
{
    public string CpuName { get; set; } = "Unknown CPU";
    public string GpuName { get; set; } = "Unknown GPU";
    public string GpuDriver { get; set; } = "Unknown";
    public GpuVendor GpuVendor { get; set; } = GpuVendor.Unknown;
    public bool GpuPresent { get; set; }

    public double CpuUsage { get; set; }
    public double CpuTemp { get; set; }
    public double CpuFreqGhz { get; set; }
    public double? CpuPowerW { get; set; }

    public double GpuUsage { get; set; }
    public double GpuTemp { get; set; }
    public double? GpuFreqMhz { get; set; }
    public double? GpuPowerW { get; set; }
    public double? GpuVramUsedGb { get; set; }
    public double? GpuVramTotalGb { get; set; }

    public int CpuFanRpm { get; set; }
    public int GpuFanRpm { get; set; }

    public double RamUsage { get; set; }
    public string RamTotal { get; set; } = "Unknown";

    public bool HasBattery { get; set; }
    public int BatteryPercent { get; set; }
    public string BatteryStatus { get; set; } = "Unknown";
    public string BatteryTimeRemaining { get; set; } = "—";
    public double? BatteryHealthPct { get; set; }
    public double? BatteryPowerW { get; set; }
    public double? BatteryEnergyWh { get; set; }
    public double? BatteryDesignWh { get; set; }
    public int? BatteryCycles { get; set; }
    public bool IsPluggedIn { get; set; }

    public string Model { get; set; } = "Unknown";
    public string OsVersion { get; set; } = "Unknown";
    public string KernelVersion { get; set; } = "Unknown";

    public string CpuTempSource { get; set; } = "Not detected";
    public string GpuSource { get; set; } = "Not detected";
    public string FanSource { get; set; } = "Not detected";
    public string BatterySource { get; set; } = "Not detected";
}

/// <summary>
///     Reads CPU / GPU / fan / battery metrics from sysfs and vendor tools.
///     Paths and expensive probes are cached; safe to call from any thread.
/// </summary>
public static class MetricsService
{
    private static readonly object Sync = new();

    private static readonly Dictionary<string, string> Cache = new();

    private static bool _initialized;
    private static string _cpuName = "Unknown CPU";
    private static string _gpuName = "Unknown GPU";
    private static string _gpuDriver = "Unknown";
    private static GpuVendor _gpuVendor = GpuVendor.Unknown;
    private static bool _gpuPresent;

    private static double _lastCpuTotal, _lastCpuIdle;
    private static bool _hasLastCpu;

    private static string? _raplPath;
    private static double _lastRaplUj;
    private static DateTime _lastRaplTime = DateTime.MinValue;

    private static string? _batteryDir;
    private static bool _hasBattery;

    private static string? _fan1Path, _fan2Path;
    private static string? _fanSource;

    private static string _model = "Unknown";

    public static void Refresh()
    {
        lock (Sync)
        {
            Cache.Clear();
            _initialized = false;
            _hasLastCpu = false;
            _raplPath = null;
            _lastRaplTime = DateTime.MinValue;
            _fan1Path = _fan2Path = null;
            Initialize();
        }
    }

    private static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _cpuName = ReadCpuName();
            DetectGpu();
            _model = ReadModel();
            DetectBattery();
            DetectFans();
            DetectRapl();
        }
        catch
        {
            // Static detection must never take the GUI down.
        }
    }

    public static SystemSnapshot Read()
    {
        lock (Sync)
        {
            Initialize();
            var snap = new SystemSnapshot
            {
                CpuName = _cpuName,
                GpuName = _gpuName,
                GpuDriver = _gpuDriver,
                GpuVendor = _gpuVendor,
                GpuPresent = _gpuPresent,
                Model = _model,
                OsVersion = _osVersionCache ??= ReadOsVersion(),
                KernelVersion = ReadKernelVersion()
            };

            snap.CpuUsage = ReadCpuUsage();
            (snap.CpuTemp, snap.CpuTempSource) = ReadCpuTemperature();
            snap.CpuFreqGhz = ReadCpuFrequencyGhz();
            snap.CpuPowerW = ReadCpuPower();

            ReadGpu(snap);

            var (cpuFan, gpuFan) = ReadFans();
            snap.CpuFanRpm = cpuFan;
            // Keep a vendor-tool fan reading (nvidia-smi) when no hwmon fan2 exists.
            if (gpuFan > 0 || snap.GpuFanRpm == 0) snap.GpuFanRpm = gpuFan;
            snap.FanSource = _fanSource ?? "Not detected";

            (snap.RamUsage, snap.RamTotal) = ReadRam();
            ReadBattery(snap);

            return snap;
        }
    }

    private static string? _osVersionCache;

    // ------------------------------------------------------------------ CPU

    private static string ReadCpuName()
    {
        try
        {
            var info = File.ReadAllText("/proc/cpuinfo");
            var match = Regex.Match(info, @"model name\s+:\s+(.+)");
            if (match.Success) return match.Groups[1].Value.Trim();
        }
        catch
        {
            // ignored
        }

        return "Unknown CPU";
    }

    private static double ReadCpuUsage()
    {
        try
        {
            var stat = File.ReadAllText("/proc/stat");
            var match = Regex.Match(stat, @"^cpu\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)", RegexOptions.Multiline);
            if (!match.Success) return 0;

            var user = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var nice = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var system = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            var idle = double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);

            var total = user + nice + system + idle;

            if (!_hasLastCpu)
            {
                _lastCpuTotal = total;
                _lastCpuIdle = idle;
                _hasLastCpu = true;
                return 0;
            }

            var totalDelta = total - _lastCpuTotal;
            var idleDelta = idle - _lastCpuIdle;
            _lastCpuTotal = total;
            _lastCpuIdle = idle;

            if (totalDelta <= 0) return 0;
            return Math.Round(Math.Clamp((1 - idleDelta / totalDelta) * 100, 0, 100), 1);
        }
        catch
        {
            return 0;
        }
    }

    private static (double temp, string source) ReadCpuTemperature()
    {
        try
        {
            if (Cache.TryGetValue("cpu_temp", out var cached) &&
                TryReadMillidegree(cached, out var temp))
                return (Math.Round(temp, 1), ShortPath(cached));

            var path = FindCpuTemperaturePath();
            if (path != null)
            {
                Cache["cpu_temp"] = path;
                if (TryReadMillidegree(path, out temp))
                    return (Math.Round(temp, 1), ShortPath(path));
            }

            var output = RunCommand("sensors", "");
            var match = Regex.Match(output, @"(?:Package id \d+|Tctl|Tdie):\s+\+?(\d+(?:\.\d+)?)°C");
            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out temp))
                return (Math.Round(temp, 1), "lm-sensors");
        }
        catch
        {
            // ignored
        }

        return (0, "Not detected");
    }

    private static bool TryReadMillidegree(string path, out double celsius)
    {
        celsius = 0;
        try
        {
            if (!File.Exists(path)) return false;
            if (!int.TryParse(File.ReadAllText(path).Trim(), out var value)) return false;
            celsius = value / 1000.0;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindCpuTemperaturePath()
    {
        var intel = FindHwmonTemp("coretemp",
            label => Regex.IsMatch(label, @"^Package id \d+$", RegexOptions.IgnoreCase));
        if (intel != null) return intel;

        var amd = FindHwmonTemp("k10temp",
            label => label.Equals("Tctl", StringComparison.OrdinalIgnoreCase) ||
                     label.Equals("Tdie", StringComparison.OrdinalIgnoreCase));
        if (amd != null) return amd;

        return FindHwmonTemp("zenpower",
            label => label.Equals("Tctl", StringComparison.OrdinalIgnoreCase) ||
                     label.Equals("Tdie", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindHwmonTemp(string hwmonName, Func<string, bool> labelMatches)
    {
        try
        {
            if (!Directory.Exists("/sys/class/hwmon")) return null;

            foreach (var dir in Directory.GetDirectories("/sys/class/hwmon").OrderBy(p => p))
            {
                var nameFile = Path.Combine(dir, "name");
                if (!File.Exists(nameFile)) continue;
                if (!File.ReadAllText(nameFile).Trim()
                        .Equals(hwmonName, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var labelFile in Directory.GetFiles(dir, "temp*_label").OrderBy(p => p))
                {
                    if (!labelMatches(File.ReadAllText(labelFile).Trim())) continue;
                    var input = Path.Combine(dir,
                        Path.GetFileName(labelFile).Replace("_label", "_input", StringComparison.Ordinal));
                    if (File.Exists(input)) return input;
                }

                var temp1 = Path.Combine(dir, "temp1_input");
                if (File.Exists(temp1)) return temp1;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static double ReadCpuFrequencyGhz()
    {
        try
        {
            var dirs = Directory.GetDirectories("/sys/devices/system/cpu", "cpu[0-9]*");
            double sum = 0;
            var count = 0;

            foreach (var dir in dirs)
            {
                var freqFile = Path.Combine(dir, "cpufreq", "scaling_cur_freq");
                if (!File.Exists(freqFile)) continue;
                if (long.TryParse(File.ReadAllText(freqFile).Trim(), out var khz))
                {
                    sum += khz / 1_000_000.0;
                    count++;
                }
            }

            if (count > 0) return Math.Round(sum / count, 2);
        }
        catch
        {
            // ignored
        }

        try
        {
            var info = File.ReadAllText("/proc/cpuinfo");
            var matches = Regex.Matches(info, @"cpu MHz\s+:\s+([\d.]+)");
            if (matches.Count == 0) return 0;
            double sum = 0;
            foreach (Match m in matches)
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
                    sum += mhz;
            return Math.Round(sum / matches.Count / 1000.0, 2);
        }
        catch
        {
            return 0;
        }
    }

    private static void DetectRapl()
    {
        try
        {
            var candidates = new List<string>();

            void Consider(string path)
            {
                if (File.Exists(path)) candidates.Add(path);
            }

            Consider("/sys/class/powercap/intel-rapl/intel-rapl:0/energy_uj");
            Consider("/sys/class/powercap/intel-rapl:0/energy_uj");
            Consider("/sys/class/powercap/amd-rapl:0/energy_uj");

            if (candidates.Count == 0 && Directory.Exists("/sys/class/powercap"))
                foreach (var dir in Directory.GetDirectories("/sys/class/powercap"))
                {
                    var name = Path.GetFileName(dir);
                    if (!name.Contains("rapl", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Count(c => c == ':') > 1) continue; // skip subzones
                    var candidate = Path.Combine(dir, "energy_uj");
                    if (File.Exists(candidate)) candidates.Add(candidate);
                }

            _raplPath = candidates.FirstOrDefault();
        }
        catch
        {
            _raplPath = null;
        }
    }

    private static double? ReadCpuPower()
    {
        try
        {
            if (_raplPath == null) return null;

            var text = File.ReadAllText(_raplPath).Trim();
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var energyUj))
                return null;

            var now = DateTime.UtcNow;

            if (_lastRaplTime == DateTime.MinValue)
            {
                _lastRaplUj = energyUj;
                _lastRaplTime = now;
                return null;
            }

            var seconds = (now - _lastRaplTime).TotalSeconds;
            var delta = energyUj - _lastRaplUj;
            _lastRaplUj = energyUj;
            _lastRaplTime = now;

            if (seconds <= 0.5 || delta < 0) return null;
            return Math.Round(delta / 1_000_000.0 / seconds, 1);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ GPU

    private static void DetectGpu()
    {
        try
        {
            var lspci = RunCommand("lspci", "");

            if (Directory.Exists("/sys/class/drm/card0/device/driver/module/nvidia") ||
                lspci.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                File.Exists("/proc/driver/nvidia/version"))
            {
                _gpuVendor = GpuVendor.Nvidia;
                _gpuPresent = true;
                _gpuName = ReadNvidiaName() ?? "NVIDIA GPU";
                var driver = RunCommand("nvidia-smi", "--query-gpu=driver_version --format=csv,noheader");
                _gpuDriver = string.IsNullOrWhiteSpace(driver) ? "Unknown" : driver.Trim();
                return;
            }

            if (lspci.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                lspci.Contains("ATI", StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists("/sys/class/drm/card0/device/driver/module/amdgpu"))
            {
                _gpuVendor = GpuVendor.Amd;
                _gpuPresent = true;
                _gpuName = ReadLspciGpuName(lspci, "AMD") ?? "AMD Radeon Graphics";
                _gpuDriver = ReadAmdDriverVersion() ?? "amdgpu";
                return;
            }

            if (lspci.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            {
                _gpuVendor = GpuVendor.Intel;
                _gpuPresent = true;
                _gpuName = ReadLspciGpuName(lspci, "Intel") ?? "Intel Graphics";
                _gpuDriver = "i915 / xe";
                return;
            }

            _gpuVendor = GpuVendor.Unknown;
            _gpuPresent = false;
            _gpuName = "No discrete GPU detected";
            _gpuDriver = "—";
        }
        catch
        {
            _gpuVendor = GpuVendor.Unknown;
            _gpuPresent = false;
        }
    }

    private static string? ReadNvidiaName()
    {
        var output = RunCommand("nvidia-smi", "--query-gpu=name --format=csv,noheader");
        return string.IsNullOrWhiteSpace(output) ? null : output.Trim().Split('\n')[0].Trim();
    }

    private static string? ReadLspciGpuName(string lspci, string vendor)
    {
        try
        {
            foreach (var line in lspci.Split('\n'))
            {
                if (!line.Contains("VGA", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("3D controller", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!line.Contains(vendor, StringComparison.OrdinalIgnoreCase)) continue;

                var match = Regex.Match(line, @":\s+(.+?)(?:\s*\(rev|\s*\[)");
                var name = match.Success ? match.Groups[1].Value : line;
                return Regex.Replace(name, @"\b(R[0-9]{3}|GFX[0-9]{3}|UHD Graphics|Iris Xe|Alder Lake|Raptor Lake)\b", "").Trim();
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string? ReadAmdDriverVersion()
    {
        try
        {
            var versionFile = "/sys/module/amdgpu/version";
            if (File.Exists(versionFile)) return "amdgpu " + File.ReadAllText(versionFile).Trim();
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static void ReadGpu(SystemSnapshot snap)
    {
        if (!_gpuPresent)
        {
            snap.GpuSource = "No GPU detected";
            return;
        }

        switch (_gpuVendor)
        {
            case GpuVendor.Nvidia:
                ReadNvidia(snap);
                break;
            case GpuVendor.Amd:
                ReadAmd(snap);
                break;
            case GpuVendor.Intel:
                ReadIntel(snap);
                break;
        }
    }

    private static void ReadNvidia(SystemSnapshot snap)
    {
        var output = RunCommand("nvidia-smi",
            "--query-gpu=temperature.gpu,utilization.gpu,clocks.current.graphics,power.draw,memory.used,memory.total,fan.speed --format=csv,noheader,nounits");

        if (string.IsNullOrWhiteSpace(output))
        {
            snap.GpuSource = "nvidia-smi unavailable";
            return;
        }

        try
        {
            var first = output.Split('\n')[0].Trim();
            var parts = first.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length < 6) return;

            snap.GpuTemp = ParseDouble(parts[0]);
            snap.GpuUsage = ParseDouble(parts[1]);
            snap.GpuFreqMhz = ParseDoubleOrNull(parts[2]);
            snap.GpuPowerW = ParseDoubleOrNull(parts[3]);
            var usedMb = ParseDoubleOrNull(parts[4]);
            var totalMb = ParseDoubleOrNull(parts[5]);
            if (usedMb.HasValue) snap.GpuVramUsedGb = Math.Round(usedMb.Value / 1024.0, 2);
            if (totalMb.HasValue) snap.GpuVramTotalGb = Math.Round(totalMb.Value / 1024.0, 1);

            var nvFan = parts.Length > 6 ? ParseDoubleOrNull(parts[6]) : null;
            if (nvFan.HasValue) snap.GpuFanRpm = (int)Math.Round(nvFan.Value * 60);

            snap.GpuSource = "nvidia-smi";
        }
        catch
        {
            snap.GpuSource = "nvidia-smi parse error";
        }
    }

    private static void ReadAmd(SystemSnapshot snap)
    {
        try
        {
            var device = FindAmdDevicePath();
            if (device == null)
            {
                snap.GpuSource = "amdgpu sysfs not found";
                return;
            }

            var busy = Path.Combine(device, "gpu_busy_percent");
            if (File.Exists(busy) && double.TryParse(File.ReadAllText(busy).Trim(), out var usage))
                snap.GpuUsage = usage;

            var vramUsed = Path.Combine(device, "mem_info_vram_used");
            var vramTotal = Path.Combine(device, "mem_info_vram_total");
            if (File.Exists(vramUsed) && long.TryParse(File.ReadAllText(vramUsed).Trim(), out var usedBytes))
                snap.GpuVramUsedGb = Math.Round(usedBytes / 1024.0 / 1024.0 / 1024.0, 2);
            if (File.Exists(vramTotal) && long.TryParse(File.ReadAllText(vramTotal).Trim(), out var totalBytes))
                snap.GpuVramTotalGb = Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 1);

            var hwmon = Path.Combine(device, "hwmon");
            if (Directory.Exists(hwmon))
                foreach (var dir in Directory.GetDirectories(hwmon))
                {
                    var tempFile = Path.Combine(dir, "temp1_input");
                    if (snap.GpuTemp == 0 && File.Exists(tempFile) &&
                        int.TryParse(File.ReadAllText(tempFile).Trim(), out var milli))
                        snap.GpuTemp = Math.Round(milli / 1000.0, 1);

                    var powerFile = Path.Combine(dir, "power1_average");
                    if (File.Exists(powerFile) &&
                        double.TryParse(File.ReadAllText(powerFile).Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var microW))
                        snap.GpuPowerW = Math.Round(microW / 1_000_000.0, 1);
                }

            var sclk = Path.Combine(device, "pp_dpm_sclk");
            if (File.Exists(sclk))
            {
                var active = File.ReadAllLines(sclk).FirstOrDefault(l => l.TrimEnd().EndsWith("*"));
                if (active != null)
                {
                    var m = Regex.Match(active, @"(\d+)\s*Mhz", RegexOptions.IgnoreCase);
                    if (m.Success) snap.GpuFreqMhz = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }

            snap.GpuSource = "amdgpu sysfs";
        }
        catch
        {
            snap.GpuSource = "amdgpu read error";
        }
    }

    private static string? FindAmdDevicePath()
    {
        try
        {
            foreach (var card in Directory.GetDirectories("/sys/class/drm", "card[0-9]*"))
            {
                var device = Path.Combine(card, "device");
                if (File.Exists(Path.Combine(device, "gpu_busy_percent"))) return device;
            }

            foreach (var card in Directory.GetDirectories("/sys/class/drm", "card[0-9]*"))
            {
                var driver = Path.Combine(card, "device", "driver");
                if (!Directory.Exists(driver)) continue;
                var target = new DirectoryInfo(driver).LinkTarget ?? "";
                if (target.Contains("amdgpu")) return Path.Combine(card, "device");
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static void ReadIntel(SystemSnapshot snap)
    {
        try
        {
            foreach (var card in Directory.GetDirectories("/sys/class/drm", "card[0-9]*"))
            {
                var device = Path.Combine(card, "device");
                var busy = Path.Combine(device, "gpu_busy_percent");
                if (File.Exists(busy) && double.TryParse(File.ReadAllText(busy).Trim(), out var usage))
                    snap.GpuUsage = usage;

                var hwmon = Path.Combine(device, "hwmon");
                if (Directory.Exists(hwmon))
                    foreach (var dir in Directory.GetDirectories(hwmon))
                    {
                        var tempFile = Path.Combine(dir, "temp1_input");
                        if (File.Exists(tempFile) &&
                            int.TryParse(File.ReadAllText(tempFile).Trim(), out var milli))
                            snap.GpuTemp = Math.Round(milli / 1000.0, 1);
                    }

                if (snap.GpuTemp > 0 || snap.GpuUsage > 0)
                {
                    snap.GpuSource = "i915/xe sysfs";
                    return;
                }
            }

            snap.GpuSource = "i915/xe sysfs unavailable";
        }
        catch
        {
            snap.GpuSource = "Intel GPU read error";
        }
    }

    // ----------------------------------------------------------------- Fans

    private static void DetectFans()
    {
        try
        {
            if (Directory.Exists("/sys/class/hwmon"))
                foreach (var dir in Directory.GetDirectories("/sys/class/hwmon"))
                {
                    var nameFile = Path.Combine(dir, "name");
                    if (!File.Exists(nameFile)) continue;
                    var name = File.ReadAllText(nameFile).Trim().ToLowerInvariant();

                    var fan1 = Path.Combine(dir, "fan1_input");
                    var fan2 = Path.Combine(dir, "fan2_input");

                    var isAcer = name.Contains("acer") || name.Contains("linuwu") || name.Contains("nitro");
                    var isGeneric = name.Contains("it87") || name.Contains("nct") || name.Contains("asus");

                    if ((isAcer || isGeneric) && File.Exists(fan1))
                    {
                        _fan1Path = fan1;
                        if (File.Exists(fan2)) _fan2Path = fan2;
                        _fanSource = $"{name} ({dir})";
                        if (isAcer) return;
                    }
                }

            // Acer platform fallbacks
            foreach (var baseDir in new[] { "/sys/devices/platform/acer-wmi", "/sys/devices/platform/acer_wmi" })
            {
                var fan1 = Path.Combine(baseDir, "fan1_input");
                var fan2 = Path.Combine(baseDir, "fan2_input");
                if (File.Exists(fan1))
                {
                    _fan1Path = fan1;
                    if (File.Exists(fan2)) _fan2Path = fan2;
                    _fanSource = baseDir;
                    return;
                }
            }

            // Last resort: any hwmon exposing two fans
            if (Directory.Exists("/sys/class/hwmon"))
                foreach (var dir in Directory.GetDirectories("/sys/class/hwmon"))
                {
                    var fan1 = Path.Combine(dir, "fan1_input");
                    var fan2 = Path.Combine(dir, "fan2_input");
                    if (File.Exists(fan1) && File.Exists(fan2))
                    {
                        _fan1Path = fan1;
                        _fan2Path = fan2;
                        _fanSource = dir;
                        return;
                    }
                }

            _fanSource = "Not detected";
        }
        catch
        {
            _fanSource = "Not detected";
        }
    }

    private static (int cpu, int gpu) ReadFans()
    {
        try
        {
            if (_fan1Path != null && File.Exists(_fan1Path) &&
                int.TryParse(File.ReadAllText(_fan1Path).Trim(), out var cpu))
            {
                var gpu = 0;
                if (_fan2Path != null && File.Exists(_fan2Path))
                    int.TryParse(File.ReadAllText(_fan2Path).Trim(), out gpu);
                return (cpu, gpu);
            }
        }
        catch
        {
            // ignored
        }

        return (0, 0);
    }

    // -------------------------------------------------------------- Battery

    private static void DetectBattery()
    {
        try
        {
            if (!Directory.Exists("/sys/class/power_supply"))
            {
                _hasBattery = false;
                return;
            }

            foreach (var dir in Directory.GetDirectories("/sys/class/power_supply"))
            {
                var typeFile = Path.Combine(dir, "type");
                if (!File.Exists(typeFile)) continue;
                if (!File.ReadAllText(typeFile).Trim().Equals("Battery", StringComparison.OrdinalIgnoreCase))
                    continue;

                _batteryDir = dir;
                _hasBattery = true;
                return;
            }

            _hasBattery = false;
        }
        catch
        {
            _hasBattery = false;
        }
    }

    private static void ReadBattery(SystemSnapshot snap)
    {
        snap.HasBattery = _hasBattery;
        snap.IsPluggedIn = ReadAcOnline();

        if (!_hasBattery || _batteryDir == null)
        {
            snap.BatterySource = "No battery";
            return;
        }

        try
        {
            snap.BatterySource = _batteryDir;

            if (int.TryParse(ReadSys(_batteryDir, "capacity"), out var capacity))
                snap.BatteryPercent = capacity;

            snap.BatteryStatus = ReadSys(_batteryDir, "status") ?? "Unknown";

            var energyFull = ReadLong(_batteryDir, "energy_full") ?? ReadLong(_batteryDir, "charge_full");
            var energyNow = ReadLong(_batteryDir, "energy_now") ?? ReadLong(_batteryDir, "charge_now");
            var powerNow = ReadLong(_batteryDir, "power_now") ?? ReadLong(_batteryDir, "current_now");

            if (energyFull.HasValue) snap.BatteryEnergyWh = Math.Round(energyFull.Value / 1_000_000.0, 1);
            if (powerNow.HasValue) snap.BatteryPowerW = Math.Round(powerNow.Value / 1_000_000.0, 1);

            var design = ReadLong(_batteryDir, "energy_full_design") ?? ReadLong(_batteryDir, "charge_full_design");
            if (design.HasValue) snap.BatteryDesignWh = Math.Round(design.Value / 1_000_000.0, 1);

            if (energyFull.HasValue && design is > 0)
                snap.BatteryHealthPct = Math.Round(energyFull.Value / (double)design.Value * 100.0, 1);

            if (int.TryParse(ReadSys(_batteryDir, "charge_cycle_count"), out var cycles))
                snap.BatteryCycles = cycles;

            if (snap.BatteryStatus.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                snap.BatteryStatus.Equals("Full", StringComparison.OrdinalIgnoreCase))
            {
                snap.BatteryTimeRemaining = snap.BatteryStatus;
            }
            else if (energyNow.HasValue && powerNow is > 0)
            {
                snap.BatteryTimeRemaining =
                    $"{energyNow.Value / (double)powerNow.Value:F1} h";
            }
        }
        catch
        {
            // Keep whatever was read so far.
        }
    }

    private static bool ReadAcOnline()
    {
        foreach (var path in new[]
                 {
                     "/sys/class/power_supply/AC/online",
                     "/sys/class/power_supply/ACAD/online",
                     "/sys/class/power_supply/ADP1/online",
                     "/sys/class/power_supply/AC0/online"
                 })
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path).Trim() == "1";
            }
            catch
            {
                // ignored
            }

        return false;
    }

    // ------------------------------------------------------------------ Misc

    private static (double usage, string total) ReadRam()
    {
        try
        {
            var memInfo = File.ReadAllText("/proc/meminfo");
            var totalMatch = Regex.Match(memInfo, @"MemTotal:\s+(\d+) kB");
            var availableMatch = Regex.Match(memInfo, @"MemAvailable:\s+(\d+) kB");
            if (!totalMatch.Success || !availableMatch.Success) return (0, "Unknown");

            var totalKb = long.Parse(totalMatch.Groups[1].Value);
            var availableKb = long.Parse(availableMatch.Groups[1].Value);
            var usage = Math.Round((totalKb - availableKb) / (double)totalKb * 100.0, 1);
            var totalGb = totalKb / 1024.0 / 1024.0;
            return (usage, $"{totalGb:F1} GB");
        }
        catch
        {
            return (0, "Unknown");
        }
    }

    private static string ReadModel()
    {
        try
        {
            if (File.Exists("/sys/class/dmi/id/product_name"))
                return File.ReadAllText("/sys/class/dmi/id/product_name").Trim();
        }
        catch
        {
            // ignored
        }

        return "Unknown";
    }

    private static string ReadOsVersion()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                var match = Regex.Match(File.ReadAllText("/etc/os-release"), @"PRETTY_NAME=""(.+?)""");
                if (match.Success) return match.Groups[1].Value;
            }
        }
        catch
        {
            // ignored
        }

        return "Unknown";
    }

    private static string ReadKernelVersion()
    {
        try
        {
            return File.ReadAllText("/proc/sys/kernel/osrelease").Trim();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string? ReadSys(string dir, string file)
    {
        try
        {
            var path = Path.Combine(dir, file);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static long? ReadLong(string dir, string file)
    {
        var value = ReadSys(dir, file);
        return long.TryParse(value, out var parsed) ? parsed : null;
    }

    private static double ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static double? ParseDoubleOrNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains("N/A", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("[Not Supported]", StringComparison.OrdinalIgnoreCase))
            return null;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string ShortPath(string path)
    {
        var idx = path.IndexOf("/hwmon", StringComparison.Ordinal);
        return idx >= 0 ? path[(idx + 1)..] : path;
    }

    public static string RunCommand(string command, string arguments, int timeoutMs = 2000)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(info);
            if (process == null) return string.Empty;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
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

            Task.WaitAll(new Task[] { outputTask, errorTask }, 400);
            return outputTask.IsCompletedSuccessfully ? outputTask.Result : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
