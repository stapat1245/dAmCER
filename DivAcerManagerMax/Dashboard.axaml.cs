using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DivAcerManagerMax.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Material.Icons;
using Material.Icons.Avalonia;
using SkiaSharp;

namespace DivAcerManagerMax;

public partial class Dashboard : UserControl, INotifyPropertyChanged
{
    private const int MaxHistoryPoints = 60;
    private const int MinRpmForSpin = 150;

    private static readonly IBrush BrushOk = new SolidColorBrush(Color.Parse("#38C793"));
    private static readonly IBrush BrushWarn = new SolidColorBrush(Color.Parse("#E9A83C"));
    private static readonly IBrush BrushHot = new SolidColorBrush(Color.Parse("#F0595E"));

    private readonly ObservableCollection<double> _cpuTempHistory = new();
    private readonly ObservableCollection<double> _gpuTempHistory = new();
    private readonly ObservableCollection<double> _cpuUsageHistory = new();
    private readonly ObservableCollection<double> _gpuUsageHistory = new();
    private readonly ObservableCollection<double> _cpuFanHistory = new();
    private readonly ObservableCollection<double> _gpuFanHistory = new();

    private readonly DispatcherTimer _spinTimer;
    private bool _spinTimerRunning;

    private readonly RotateTransform _cpuFanTransform = new();
    private readonly RotateTransform _gpuFanTransform = new();

    private readonly List<ISeries> _tempSeries;
    private readonly List<ISeries> _usageSeries;
    private readonly List<ISeries> _fanSeries;

    private DAMXSettings? _settings;
    private Func<string, Task>? _profileRequested;
    private Func<string, Task>? _fanModeRequested;

    private string _currentProfile = "";
    private double _cpuAngle;
    private double _gpuAngle;
    private int _cpuRpm;
    private int _gpuRpm;
    private string _chartMode = "temp";

    private string _modelText = "Detecting hardware…";
    private string _powerSourceText = "Detecting…";
    private IBrush _powerSourceBrush = BrushOk;
    private MaterialIconKind _powerSourceIcon = MaterialIconKind.PowerPlugOutline;

    private string _cpuModel = "Detecting…";
    private string _cpuTempText = "—";
    private double _cpuTempValue;
    private IBrush _cpuTempBrush = BrushOk;
    private string _cpuTempBadge = "—";
    private string _cpuUsageText = "—";
    private double _cpuUsageValue;
    private string _cpuFreqText = "—";
    private string _cpuPowerText = "—";

    private string _gpuModel = "Detecting…";
    private string _gpuTempText = "—";
    private double _gpuTempValue;
    private IBrush _gpuTempBrush = BrushOk;
    private string _gpuTempBadge = "—";
    private string _gpuUsageText = "—";
    private double _gpuUsageValue;
    private string _gpuFreqText = "—";
    private string _gpuPowerText = "—";
    private bool _vramVisible;
    private string _vramText = "—";
    private double _vramValue;

    private string _ramUsageText = "—";
    private double _ramUsageValue;
    private string _ramTotalText = "—";
    private string _osText = "—";
    private string _kernelText = "—";

    private string _fanModeText = "Auto";
    private string _cpuFanText = "—";
    private string _gpuFanText = "—";
    private string _fanSourceText = "";

    private bool _hasBattery;
    private string _batteryPercentText = "—";
    private double _batteryPercentValue;
    private string _batteryStatusText = "Unknown";
    private string _batteryTimeText = "—";
    private string _batteryHealthText = "—";
    private string _batteryCyclesText = "—";
    private string _batteryPowerText = "—";

    private string _currentProfileText = "Balanced";
    private string _profileHintText = "Profiles follow AC / battery state.";
    private string _daemonVersionText = "—";
    private string _driverVersionText = "—";
    private string _featureCountText = "—";

    public Dashboard()
    {
        InitializeComponent();

        CpuFanIcon.RenderTransform = _cpuFanTransform;
        GpuFanIcon.RenderTransform = _gpuFanTransform;

        _tempSeries = new List<ISeries>
        {
            Line(_cpuTempHistory, "CPU", SKColor.Parse("#3D9BF7")),
            Line(_gpuTempHistory, "GPU", SKColor.Parse("#38C793"))
        };
        _usageSeries = new List<ISeries>
        {
            Line(_cpuUsageHistory, "CPU", SKColor.Parse("#3D9BF7")),
            Line(_gpuUsageHistory, "GPU", SKColor.Parse("#E9A83C"))
        };
        _fanSeries = new List<ISeries>
        {
            Line(_cpuFanHistory, "CPU fan", SKColor.Parse("#3D9BF7")),
            Line(_gpuFanHistory, "GPU fan", SKColor.Parse("#38C793"))
        };

        SetChartMode("temp");

        DataContext = this;

        // One shared poller feeds every page; the fan spin animation only runs
        // while this page is visible and the fans are actually turning.
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _spinTimer.Tick += (_, _) => SpinFans();

        MetricsPoller.Updated += OnMetricsUpdated;
        DetachedFromVisualTree += (_, _) =>
        {
            MetricsPoller.Updated -= OnMetricsUpdated;
            _spinTimer.Stop();
            _spinTimerRunning = false;
        };
    }

    private void OnMetricsUpdated(SystemSnapshot snapshot)
    {
        Dispatcher.UIThread.Post(() => Apply(snapshot));
    }

    // ------------------------------------------------------------ delegates

    public void Configure(
        DAMXSettings? settings,
        Func<string, Task>? profileRequested,
        Func<string, Task>? fanModeRequested)
    {
        _settings = settings;
        _profileRequested = profileRequested;
        _fanModeRequested = fanModeRequested;

        DaemonVersionText = settings == null ? "—" : $"v{settings.Version}";
        DriverVersionText = settings == null ? "—" : $"v{settings.DriverVersion}";
        FeatureCountText = settings?.AvailableFeatures == null
            ? "—"
            : $"{settings.AvailableFeatures.Count} supported";

        BuildProfileChips();
        BuildFanModeChips();
    }

    public void SetActiveProfile(string profile)
    {
        _currentProfile = profile ?? "";
        CurrentProfileText = ProfileLabel(_currentProfile);
        foreach (var child in ProfileChipsPanel.Children.OfType<Button>())
            child.Classes.Set("active", string.Equals((string?)child.Tag, _currentProfile,
                StringComparison.OrdinalIgnoreCase));
    }

    public void SetActiveFanMode(string mode)
    {
        foreach (var child in FanModeChipsPanel.Children.OfType<Button>())
            child.Classes.Set("active", string.Equals((string?)child.Tag, mode, StringComparison.OrdinalIgnoreCase));

        FanModeText = mode.ToLowerInvariant() switch
        {
            "max" => "Maximum cooling",
            "manual" => "Manual speed",
            "curve" => "Custom fan curve",
            _ => "Automatic (EC controlled)"
        };
    }

    private void BuildProfileChips()
    {
        ProfileChipsPanel.Children.Clear();
        var available = _settings?.ThermalProfile?.Available ?? new List<string>();

        foreach (var profile in available)
        {
            var chip = new Button
            {
                Content = ProfileLabel(profile),
                Tag = profile,
                Classes = { "Chip" }
            };
            chip.Click += async (_, _) =>
            {
                try
                {
                    if (_profileRequested != null) await _profileRequested(profile);
                }
                catch
                {
                    // MainWindow reports failures through toasts.
                }
            };
            ProfileChipsPanel.Children.Add(chip);
        }

        if (ProfileChipsPanel.Children.Count == 0)
            ProfileChipsPanel.Children.Add(new TextBlock
            {
                Text = "No profiles reported by daemon",
                Classes = { "Dim" },
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });

        SetActiveProfile(_settings?.ThermalProfile?.Current ?? "");
    }

    private void BuildFanModeChips()
    {
        FanModeChipsPanel.Children.Clear();
        AddFanChip("auto", "Auto");
        AddFanChip("max", "Max");
        AddFanChip("manual", "Manual");
    }

    private void AddFanChip(string mode, string label)
    {
        var chip = new Button { Content = label, Tag = mode, Classes = { "Chip" } };
        chip.Click += async (_, _) =>
        {
            try
            {
                if (_fanModeRequested != null) await _fanModeRequested(mode);
            }
            catch
            {
                // MainWindow reports failures through toasts.
            }
        };
        FanModeChipsPanel.Children.Add(chip);
    }

    private static string ProfileLabel(string profile)
    {
        return profile.ToLowerInvariant() switch
        {
            "low-power" => "Eco",
            "quiet" => "Quiet",
            "balanced" => "Balanced",
            "balanced-performance" => "Performance",
            "performance" => "Turbo",
            _ => string.IsNullOrWhiteSpace(profile) ? "Balanced" : profile
        };
    }

    // ------------------------------------------------------------- metrics

    private void Apply(SystemSnapshot snap)
    {
        ModelText = snap.Model;
        CpuModel = snap.CpuName;
        GpuModel = snap.GpuPresent ? snap.GpuName : "No discrete GPU detected";

        CpuTempValue = snap.CpuTemp;
        CpuTempText = snap.CpuTemp > 0 ? $"{snap.CpuTemp:F1} °C" : "—";
        CpuTempBrush = TempBrush(snap.CpuTemp);
        CpuTempBadge = TempBadge(snap.CpuTemp);
        CpuUsageValue = snap.CpuUsage;
        CpuUsageText = $"{snap.CpuUsage:F0} %";
        CpuFreqText = snap.CpuFreqGhz > 0 ? $"{snap.CpuFreqGhz:F2} GHz" : "—";
        CpuPowerText = snap.CpuPowerW.HasValue ? $"{snap.CpuPowerW.Value:F1} W" : "—";

        GpuTempValue = snap.GpuTemp;
        GpuTempText = snap.GpuTemp > 0 ? $"{snap.GpuTemp:F1} °C" : "—";
        GpuTempBrush = TempBrush(snap.GpuTemp);
        GpuTempBadge = TempBadge(snap.GpuTemp);
        GpuUsageValue = snap.GpuUsage;
        GpuUsageText = $"{snap.GpuUsage:F0} %";
        GpuFreqText = snap.GpuFreqMhz.HasValue ? $"{snap.GpuFreqMhz.Value:F0} MHz" : "—";
        GpuPowerText = snap.GpuPowerW.HasValue ? $"{snap.GpuPowerW.Value:F1} W" : "—";

        VramVisible = snap.GpuVramUsedGb.HasValue && snap.GpuVramTotalGb.HasValue;
        if (VramVisible)
        {
            VramText = $"{snap.GpuVramUsedGb:F1} / {snap.GpuVramTotalGb:F1} GB";
            VramValue = snap.GpuVramTotalGb > 0
                ? Math.Clamp(snap.GpuVramUsedGb!.Value / snap.GpuVramTotalGb.Value * 100.0, 0, 100)
                : 0;
        }

        RamUsageValue = snap.RamUsage;
        RamUsageText = $"{snap.RamUsage:F0} %";
        RamTotalText = snap.RamTotal;
        OsText = snap.OsVersion;
        KernelText = snap.KernelVersion;

        CpuFanText = snap.CpuFanRpm > 0 ? $"{snap.CpuFanRpm:N0} RPM" : "—";
        GpuFanText = snap.GpuFanRpm > 0 ? $"{snap.GpuFanRpm:N0} RPM" : "—";
        FanSourceText = snap.FanSource;
        _cpuRpm = snap.CpuFanRpm;
        _gpuRpm = snap.GpuFanRpm;

        PowerSourceText = snap.IsPluggedIn ? "Plugged in" : "On battery";
        PowerSourceBrush = snap.IsPluggedIn ? BrushOk : BrushWarn;
        _powerSourceIcon = snap.IsPluggedIn ? MaterialIconKind.PowerPlugOutline : MaterialIconKind.BatteryOutline;
        UpdatePowerIcon();

        HasBattery = snap.HasBattery;
        BatteryPercentValue = snap.BatteryPercent;
        BatteryPercentText = snap.HasBattery ? $"{snap.BatteryPercent} %" : "—";
        BatteryStatusText = snap.BatteryStatus;
        BatteryTimeText = snap.BatteryTimeRemaining;
        BatteryHealthText = snap.BatteryHealthPct.HasValue ? $"{snap.BatteryHealthPct:F1} %" : "—";
        BatteryCyclesText = snap.BatteryCycles.HasValue ? snap.BatteryCycles.Value.ToString() : "—";
        BatteryPowerText = snap.BatteryPowerW.HasValue ? $"{snap.BatteryPowerW:F1} W" : "—";

        // Hidden page: never grow history or redraw charts.
        if (IsVisible)
        {
            Push(_cpuTempHistory, snap.CpuTemp);
            Push(_gpuTempHistory, snap.GpuTemp);
            Push(_cpuUsageHistory, snap.CpuUsage);
            Push(_gpuUsageHistory, snap.GpuUsage);
            Push(_cpuFanHistory, snap.CpuFanRpm);
            Push(_gpuFanHistory, snap.GpuFanRpm);
        }

        UpdateSpinTimer();
    }

    private static void Push(ObservableCollection<double> history, double value)
    {
        if (history.Count >= MaxHistoryPoints) history.RemoveAt(0);
        history.Add(value);
    }

    private static IBrush TempBrush(double temp)
    {
        if (temp <= 0) return BrushOk;
        if (temp < 60) return BrushOk;
        return temp < 80 ? BrushWarn : BrushHot;
    }

    private static string TempBadge(double temp)
    {
        if (temp <= 0) return "N/A";
        if (temp < 60) return "Cool";
        return temp < 80 ? "Warm" : "Hot";
    }

    private void UpdatePowerIcon()
    {
        PowerSourceIcon.Kind = _powerSourceIcon;
        PowerSourceIcon.Foreground = _powerSourceBrush;
    }

    // --------------------------------------------------------------- charts

    private static ISeries Line(ObservableCollection<double> values, string name, SKColor color)
    {
        return new LineSeries<double>
        {
            Values = values,
            Name = name,
            Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
            Fill = new SolidColorPaint(color.WithAlpha(26)),
            GeometrySize = 0,
            GeometryStroke = null,
            GeometryFill = null
        };
    }

    private void SetChartMode(string mode)
    {
        _chartMode = mode;

        List<ISeries> series;
        string axis;
        switch (mode)
        {
            case "usage":
                series = _usageSeries;
                axis = "%";
                break;
            case "fan":
                series = _fanSeries;
                axis = "RPM";
                break;
            default:
                series = _tempSeries;
                axis = "°C";
                break;
        }

        LiveChart.Series = series;
        LiveChart.XAxes = new[] { new Axis { IsVisible = false, TextSize = 10 } };
        LiveChart.YAxes = new[]
        {
            new Axis
            {
                Name = axis,
                NamePaint = new SolidColorPaint(SKColor.Parse("#6C6C79")),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#A7A7B4")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#23232D")) { StrokeThickness = 1 },
                TextSize = 11,
                MinLimit = mode == "usage" ? 0d : null,
                MaxLimit = mode == "usage" ? 100d : null
            }
        };

        ChartTempButton.Classes.Set("active", mode == "temp");
        ChartUsageButton.Classes.Set("active", mode == "usage");
        ChartFanButton.Classes.Set("active", mode == "fan");
        ChartHintText.Text = mode switch
        {
            "usage" => "CPU / GPU load · two-second refresh",
            "fan" => "Fan speed · two-second refresh",
            _ => "CPU / GPU temperature · two-second refresh"
        };
    }

    private void ChartTempButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetChartMode("temp");
    }

    private void ChartUsageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetChartMode("usage");
    }

    private void ChartFanButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetChartMode("fan");
    }

    // ------------------------------------------------------------- fan spin

    private void SpinFans()
    {
        if (!IsVisible) return;

        const double dt = 0.05;
        var spinning = false;

        if (_cpuRpm >= MinRpmForSpin)
        {
            _cpuAngle = (_cpuAngle + _cpuRpm / 60.0 * 0.25 * 360.0 * dt) % 360;
            _cpuFanTransform.Angle = _cpuAngle;
            spinning = true;
        }

        if (_gpuRpm >= MinRpmForSpin)
        {
            _gpuAngle = (_gpuAngle + _gpuRpm / 60.0 * 0.25 * 360.0 * dt) % 360;
            _gpuFanTransform.Angle = _gpuAngle;
            spinning = true;
        }

        if (!spinning && _spinTimerRunning)
        {
            _spinTimer.Stop();
            _spinTimerRunning = false;
        }
    }

    private void UpdateSpinTimer()
    {
        var shouldSpin = IsVisible && (_cpuRpm >= MinRpmForSpin || _gpuRpm >= MinRpmForSpin);
        if (shouldSpin && !_spinTimerRunning)
        {
            _spinTimer.Start();
            _spinTimerRunning = true;
        }
        else if (!shouldSpin && _spinTimerRunning)
        {
            _spinTimer.Stop();
            _spinTimerRunning = false;
        }
    }

    public void SetFanModeLabel(string mode)
    {
        FanModeText = mode.ToLowerInvariant() switch
        {
            "max" => "Maximum cooling",
            "manual" => "Manual speed",
            "curve" => "Custom fan curve",
            _ => "Automatic (EC controlled)"
        };
        SetActiveFanMode(mode);
    }

    public IBrush PowerSourceBrush
    {
        get => _powerSourceBrush;
        set => Set(ref _powerSourceBrush, value);
    }

    // --------------------------------------------------- INotifyPropertyChanged

    public string ModelText { get => _modelText; set => Set(ref _modelText, value); }
    public string PowerSourceText { get => _powerSourceText; set => Set(ref _powerSourceText, value); }
    public string CpuModel { get => _cpuModel; set => Set(ref _cpuModel, value); }
    public string CpuTempText { get => _cpuTempText; set => Set(ref _cpuTempText, value); }
    public double CpuTempValue { get => _cpuTempValue; set => Set(ref _cpuTempValue, value); }
    public IBrush CpuTempBrush { get => _cpuTempBrush; set => Set(ref _cpuTempBrush, value); }
    public string CpuTempBadge { get => _cpuTempBadge; set => Set(ref _cpuTempBadge, value); }
    public string CpuUsageText { get => _cpuUsageText; set => Set(ref _cpuUsageText, value); }
    public double CpuUsageValue { get => _cpuUsageValue; set => Set(ref _cpuUsageValue, value); }
    public string CpuFreqText { get => _cpuFreqText; set => Set(ref _cpuFreqText, value); }
    public string CpuPowerText { get => _cpuPowerText; set => Set(ref _cpuPowerText, value); }
    public string GpuModel { get => _gpuModel; set => Set(ref _gpuModel, value); }
    public string GpuTempText { get => _gpuTempText; set => Set(ref _gpuTempText, value); }
    public double GpuTempValue { get => _gpuTempValue; set => Set(ref _gpuTempValue, value); }
    public IBrush GpuTempBrush { get => _gpuTempBrush; set => Set(ref _gpuTempBrush, value); }
    public string GpuTempBadge { get => _gpuTempBadge; set => Set(ref _gpuTempBadge, value); }
    public string GpuUsageText { get => _gpuUsageText; set => Set(ref _gpuUsageText, value); }
    public double GpuUsageValue { get => _gpuUsageValue; set => Set(ref _gpuUsageValue, value); }
    public string GpuFreqText { get => _gpuFreqText; set => Set(ref _gpuFreqText, value); }
    public string GpuPowerText { get => _gpuPowerText; set => Set(ref _gpuPowerText, value); }
    public bool VramVisible { get => _vramVisible; set => Set(ref _vramVisible, value); }
    public string VramText { get => _vramText; set => Set(ref _vramText, value); }
    public double VramValue { get => _vramValue; set => Set(ref _vramValue, value); }
    public string RamUsageText { get => _ramUsageText; set => Set(ref _ramUsageText, value); }
    public double RamUsageValue { get => _ramUsageValue; set => Set(ref _ramUsageValue, value); }
    public string RamTotalText { get => _ramTotalText; set => Set(ref _ramTotalText, value); }
    public string OsText { get => _osText; set => Set(ref _osText, value); }
    public string KernelText { get => _kernelText; set => Set(ref _kernelText, value); }
    public string FanModeText { get => _fanModeText; set => Set(ref _fanModeText, value); }
    public string CpuFanText { get => _cpuFanText; set => Set(ref _cpuFanText, value); }
    public string GpuFanText { get => _gpuFanText; set => Set(ref _gpuFanText, value); }
    public string FanSourceText { get => _fanSourceText; set => Set(ref _fanSourceText, value); }
    public bool HasBattery { get => _hasBattery; set => Set(ref _hasBattery, value); }
    public string BatteryPercentText { get => _batteryPercentText; set => Set(ref _batteryPercentText, value); }

    public double BatteryPercentValue
    {
        get => _batteryPercentValue;
        set => Set(ref _batteryPercentValue, value);
    }

    public string BatteryStatusText { get => _batteryStatusText; set => Set(ref _batteryStatusText, value); }
    public string BatteryTimeText { get => _batteryTimeText; set => Set(ref _batteryTimeText, value); }
    public string BatteryHealthText { get => _batteryHealthText; set => Set(ref _batteryHealthText, value); }
    public string BatteryCyclesText { get => _batteryCyclesText; set => Set(ref _batteryCyclesText, value); }
    public string BatteryPowerText { get => _batteryPowerText; set => Set(ref _batteryPowerText, value); }
    public string CurrentProfileText { get => _currentProfileText; set => Set(ref _currentProfileText, value); }
    public string ProfileHintText { get => _profileHintText; set => Set(ref _profileHintText, value); }
    public string DaemonVersionText { get => _daemonVersionText; set => Set(ref _daemonVersionText, value); }
    public string DriverVersionText { get => _driverVersionText; set => Set(ref _driverVersionText, value); }
    public string FeatureCountText { get => _featureCountText; set => Set(ref _featureCountText, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
