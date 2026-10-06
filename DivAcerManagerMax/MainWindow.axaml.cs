using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Shapes = Avalonia.Controls.Shapes;
using DivAcerManagerMax.Services;
using Material.Icons;
using Material.Icons.Avalonia;

namespace DivAcerManagerMax;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string ProjectVersion = "1.1.0";
    private const string DefaultEffectColor = "#0078D7";
    private const string DefaultZone1Color = "#4287f5";
    private const string DefaultZone2Color = "#ff5733";
    private const string DefaultZone3Color = "#33ff57";
    private const string DefaultZone4Color = "#ffff01";
    private const int DirectionLeftToRight = 1;
    private const int DirectionRightToLeft = 2;

    private static readonly string AppDataFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DivAcerManagerMax");

    private static readonly string KeyboardZonePresetPath =
        Path.Combine(AppDataFolderPath, "keyboard-zone-colors.conf");

    private static readonly string KeyboardLightingEffectPresetPath =
        Path.Combine(AppDataFolderPath, "keyboard-lighting-effect.conf");

    // ------------------------------------------------------------------ state

    public DAMXClient _client;
    public DAMXSettings? _settings;
    private PowerSourceDetection? _powerDetection;

    private bool _isConnected;
    private bool _isPluggedIn;
    private bool _isCalibrating;
    private bool _isManualFanControl;
    private bool _isSettingFanSpeed;
    private bool _quitting;
    private bool _forceQuit;
    private bool _curveActive;
    private bool _suppressToggleHandlers;
    private int _cpuFanSpeed = 50;
    private int _gpuFanSpeed = 70;
    private int _keyboardBrightness = 100;
    private int _lightingSpeed = 5;
    private int _lastCurvePercent = -1;

    private FanCurveDefinition _editingCurve = new();

    private readonly Dictionary<string, ScrollViewer> _pages = new();
    private readonly List<Button> _navButtons = new();

    // ------------------------------------------------------------------ controls

    private Button _retryConnectionButton = null!;
    private Border _daemonStatusDot = null!;
    private TextBlock _daemonStatusText = null!;
    private TextBlock _daemonStatusDetail = null!;
    private Border _daemonStatusChip = null!;
    private Grid _daemonErrorGrid = null!;

    private TextBlock _pageTitle = null!;
    private TextBlock _pageSubtitle = null!;
    private TextBlock _headerPowerText = null!;
    private MaterialIcon _headerPowerIcon = null!;

    private Dashboard _overview = null!;

    private Border _thermalProfilePanel = null!;
    private Border _fanControlPanel = null!;
    private RadioButton _lowPowerProfileButton = null!;
    private RadioButton _quietProfileButton = null!;
    private RadioButton _balancedProfileButton = null!;
    private RadioButton _performanceProfileButton = null!;
    private RadioButton _turboProfileButton = null!;
    private TextBlock _thermalProfileInfoText = null!;

    private TextBlock _profileCountText = null!;
    private StackPanel _profilesListPanel = null!;
    private TextBox _profileNameTextBox = null!;

    private RadioButton _autoFanSpeedRadioButton = null!;
    private RadioButton _maxFanSpeedRadioButton = null!;
    private RadioButton _manualFanSpeedRadioButton = null!;
    private RadioButton _curveFanRadioButton = null!;
    private StackPanel _manualFanPanel = null!;
    private StackPanel _curveFanPanel = null!;
    private Slider _cpuFanSlider = null!;
    private Slider _gpuFanSlider = null!;
    private TextBlock _cpuFanTextBlock = null!;
    private TextBlock _gpuFanTextBlock = null!;
    private Button _setManualSpeedButton = null!;
    private TextBlock _manualDisabledInfoText = null!;
    private Canvas _curveCanvas = null!;
    private StackPanel _curvePointsPanel = null!;
    private StackPanel _curvePresetPanel = null!;
    private TextBlock _curveStatusText = null!;

    private TextBlock _batteryPagePercent = null!;
    private ProgressBar _batteryPageBar = null!;
    private TextBlock _batteryPageStatus = null!;
    private TextBlock _batteryPageTime = null!;
    private TextBlock _batteryHealthText = null!;
    private ProgressBar _batteryHealthBar = null!;
    private TextBlock _batteryCyclesText = null!;
    private TextBlock _batteryEnergyText = null!;
    private TextBlock _batteryPowerText = null!;
    private CheckBox _batteryLimitCheckBox = null!;
    private Border _calibrationControls = null!;
    private TextBlock _calibrationStatusTextBlock = null!;
    private Button _startCalibrationButton = null!;
    private Button _stopCalibrationButton = null!;
    private Border _limiterControls = null!;
    private Border _usbChargingPanel = null!;
    private ComboBox _usbChargingComboBox = null!;

    private WrapPanel _rgbPresetsPanel = null!;
    private Border _rgbPresetControls = null!;
    private Border _zoneColorControlPanel = null!;
    private Border _keyboardEffectsPanel = null!;
    private Border _backlightTimeoutControls = null!;
    private ColorPicker _zone1ColorPicker = null!;
    private ColorPicker _zone2ColorPicker = null!;
    private ColorPicker _zone3ColorPicker = null!;
    private ColorPicker _zone4ColorPicker = null!;
    private Border _zone1Border = null!;
    private Border _zone2Border = null!;
    private Border _zone3Border = null!;
    private Border _zone4Border = null!;
    private Slider _keyBrightnessSlider = null!;
    private TextBlock _keyBrightnessText = null!;
    private ColorPicker _lightEffectColorPicker = null!;
    private ComboBox _lightingModeComboBox = null!;
    private Slider _lightingSpeedSlider = null!;
    private TextBlock _lightSpeedTextBlock = null!;
    private RadioButton _leftToRightRadioButton = null!;
    private RadioButton _rightToLeftRadioButton = null!;
    private CheckBox _backlightTimeoutCheckBox = null!;

    private Border _lcdOverrideControls = null!;
    private Border _bootSoundControls = null!;
    private CheckBox _lcdOverrideCheckBox = null!;
    private CheckBox _bootAnimAndSoundCheckBox = null!;
    private CheckBox _closeToTrayCheckBox = null!;
    private CheckBox _notificationsCheckBox = null!;
    private TextBlock _modelNameText = null!;
    private TextBlock _laptopTypeText = null!;
    private TextBlock _osNameText = null!;
    private TextBlock _kernelText = null!;
    private TextBlock _daemonVersionText = null!;
    private TextBlock _driverVersionText = null!;
    private TextBlock _guiVersionText = null!;
    private TextBlock _supportedFeaturesTextBlock = null!;
    private TextBlock _projectVersionText = null!;

    private TextBlock _diagConnectionText = null!;
    private Border _diagConnectionChip = null!;
    private TextBlock _diagConnectionChipText = null!;
    private TextBlock _diagSocketText = null!;
    private TextBlock _diagDaemonVersionText = null!;
    private TextBlock _diagDriverVersionText = null!;
    private TextBlock _diagLaptopText = null!;
    private StackPanel _diagServicesPanel = null!;
    private TextBlock _nitroButtonStatusText = null!;
    private TextBlock _nitroButtonDetailText = null!;
    private StackPanel _sensorInventoryPanel = null!;
    private TextBox _diagnosticsLogTextBox = null!;

    private TextBlock _compatibilityModelText = null!;
    private TextBlock _compatibilityTypeText = null!;
    private Border _compatibilityStatusChip = null!;
    private TextBlock _compatibilityStatusText = null!;
    private StackPanel _compatibilityMatrixPanel = null!;
    private CheckBox _devModeToggle = null!;

    private Button _navBattery = null!;
    private Button _navKeyboard = null!;
    private Button _navPerformance = null!;

    // ------------------------------------------------------------------ ctor

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _client = new DAMXClient();
        BindControls();
        BuildPageMap();
        AttachEventHandlers();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private T Find<T>(string name) where T : Control
    {
        return this.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' not found");
    }

    private void BindControls()
    {
        _retryConnectionButton = Find<Button>("RetryConnectionButton");
        _daemonStatusDot = Find<Border>("DaemonStatusDot");
        _daemonStatusText = Find<TextBlock>("DaemonStatusText");
        _daemonStatusDetail = Find<TextBlock>("DaemonStatusDetail");
        _daemonStatusChip = Find<Border>("DaemonStatusChip");
        _daemonErrorGrid = Find<Grid>("DaemonErrorGrid");
        _pageTitle = Find<TextBlock>("PageTitle");
        _pageSubtitle = Find<TextBlock>("PageSubtitle");
        _headerPowerText = Find<TextBlock>("HeaderPowerText");
        _headerPowerIcon = Find<MaterialIcon>("HeaderPowerIcon");
        _overview = Find<Dashboard>("Overview");

        _thermalProfilePanel = Find<Border>("ThermalProfilePanel");
        _fanControlPanel = Find<Border>("FanControlPanel");
        _lowPowerProfileButton = Find<RadioButton>("LowPowerProfileButton");
        _quietProfileButton = Find<RadioButton>("QuietProfileButton");
        _balancedProfileButton = Find<RadioButton>("BalancedProfileButton");
        _performanceProfileButton = Find<RadioButton>("PerformanceProfileButton");
        _turboProfileButton = Find<RadioButton>("TurboProfileButton");
        _thermalProfileInfoText = Find<TextBlock>("ThermalProfileInfoText");

        _profileCountText = Find<TextBlock>("ProfileCountText");
        _profilesListPanel = Find<StackPanel>("ProfilesListPanel");
        _profileNameTextBox = Find<TextBox>("ProfileNameTextBox");

        _autoFanSpeedRadioButton = Find<RadioButton>("AutoFanSpeedRadioButton");
        _maxFanSpeedRadioButton = Find<RadioButton>("MaxFanSpeedRadioButton");
        _manualFanSpeedRadioButton = Find<RadioButton>("ManualFanSpeedRadioButton");
        _curveFanRadioButton = Find<RadioButton>("CurveFanRadioButton");
        _manualFanPanel = Find<StackPanel>("ManualFanPanel");
        _curveFanPanel = Find<StackPanel>("CurveFanPanel");
        _cpuFanSlider = Find<Slider>("CpuFanSlider");
        _gpuFanSlider = Find<Slider>("GpuFanSlider");
        _cpuFanTextBlock = Find<TextBlock>("CpuFanTextBlock");
        _gpuFanTextBlock = Find<TextBlock>("GpuFanTextBlock");
        _setManualSpeedButton = Find<Button>("SetManualSpeedButton");
        _manualDisabledInfoText = Find<TextBlock>("ManualDisabledInfoText");
        _curveCanvas = Find<Canvas>("CurveCanvas");
        _curvePointsPanel = Find<StackPanel>("CurvePointsPanel");
        _curvePresetPanel = Find<StackPanel>("CurvePresetPanel");
        _curveStatusText = Find<TextBlock>("CurveStatusText");

        _batteryPagePercent = Find<TextBlock>("BatteryPagePercent");
        _batteryPageBar = Find<ProgressBar>("BatteryPageBar");
        _batteryPageStatus = Find<TextBlock>("BatteryPageStatus");
        _batteryPageTime = Find<TextBlock>("BatteryPageTime");
        _batteryHealthText = Find<TextBlock>("BatteryHealthText");
        _batteryHealthBar = Find<ProgressBar>("BatteryHealthBar");
        _batteryCyclesText = Find<TextBlock>("BatteryCyclesText");
        _batteryEnergyText = Find<TextBlock>("BatteryEnergyText");
        _batteryPowerText = Find<TextBlock>("BatteryPowerText");
        _batteryLimitCheckBox = Find<CheckBox>("BatteryLimitCheckBox");
        _calibrationControls = Find<Border>("CalibrationControls");
        _calibrationStatusTextBlock = Find<TextBlock>("CalibrationStatusTextBlock");
        _startCalibrationButton = Find<Button>("StartCalibrationButton");
        _stopCalibrationButton = Find<Button>("StopCalibrationButton");
        _limiterControls = Find<Border>("LimiterControls");
        _usbChargingPanel = Find<Border>("UsbChargingPanel");
        _usbChargingComboBox = Find<ComboBox>("UsbChargingComboBox");

        _rgbPresetsPanel = Find<WrapPanel>("RgbPresetsPanel");
        _rgbPresetControls = Find<Border>("RgbPresetControls");
        _zoneColorControlPanel = Find<Border>("ZoneColorControlPanel");
        _keyboardEffectsPanel = Find<Border>("KeyboardEffectsPanel");
        _backlightTimeoutControls = Find<Border>("BacklightTimeoutControls");
        _zone1ColorPicker = Find<ColorPicker>("Zone1ColorPicker");
        _zone2ColorPicker = Find<ColorPicker>("Zone2ColorPicker");
        _zone3ColorPicker = Find<ColorPicker>("Zone3ColorPicker");
        _zone4ColorPicker = Find<ColorPicker>("Zone4ColorPicker");
        _zone1Border = Find<Border>("Zone1Border");
        _zone2Border = Find<Border>("Zone2Border");
        _zone3Border = Find<Border>("Zone3Border");
        _zone4Border = Find<Border>("Zone4Border");
        _keyBrightnessSlider = Find<Slider>("KeyBrightnessSlider");
        _keyBrightnessText = Find<TextBlock>("KeyBrightnessText");
        _lightEffectColorPicker = Find<ColorPicker>("LightEffectColorPicker");
        _lightingModeComboBox = Find<ComboBox>("LightingModeComboBox");
        _lightingSpeedSlider = Find<Slider>("LightingSpeedSlider");
        _lightSpeedTextBlock = Find<TextBlock>("LightSpeedTextBlock");
        _leftToRightRadioButton = Find<RadioButton>("LeftToRightRadioButton");
        _rightToLeftRadioButton = Find<RadioButton>("RightToLeftRadioButton");
        _backlightTimeoutCheckBox = Find<CheckBox>("BacklightTimeoutCheckBox");

        _lcdOverrideControls = Find<Border>("LcdOverrideControls");
        _bootSoundControls = Find<Border>("BootSoundControls");
        _lcdOverrideCheckBox = Find<CheckBox>("LcdOverrideCheckBox");
        _bootAnimAndSoundCheckBox = Find<CheckBox>("BootAnimAndSoundCheckBox");
        _closeToTrayCheckBox = Find<CheckBox>("CloseToTrayCheckBox");
        _notificationsCheckBox = Find<CheckBox>("NotificationsCheckBox");
        _modelNameText = Find<TextBlock>("ModelNameText");
        _laptopTypeText = Find<TextBlock>("LaptopTypeText");
        _osNameText = Find<TextBlock>("OsNameText");
        _kernelText = Find<TextBlock>("KernelText");
        _daemonVersionText = Find<TextBlock>("DaemonVersionText");
        _driverVersionText = Find<TextBlock>("DriverVersionText");
        _guiVersionText = Find<TextBlock>("GuiVersionText");
        _supportedFeaturesTextBlock = Find<TextBlock>("SupportedFeaturesTextBlock");
        _projectVersionText = Find<TextBlock>("ProjectVersionText");

        _diagConnectionText = Find<TextBlock>("DiagConnectionText");
        _diagConnectionChip = Find<Border>("DiagConnectionChip");
        _diagConnectionChipText = Find<TextBlock>("DiagConnectionChipText");
        _diagSocketText = Find<TextBlock>("DiagSocketText");
        _diagDaemonVersionText = Find<TextBlock>("DiagDaemonVersionText");
        _diagDriverVersionText = Find<TextBlock>("DiagDriverVersionText");
        _diagLaptopText = Find<TextBlock>("DiagLaptopText");
        _diagServicesPanel = Find<StackPanel>("DiagServicesPanel");
        _nitroButtonStatusText = Find<TextBlock>("NitroButtonStatusText");
        _nitroButtonDetailText = Find<TextBlock>("NitroButtonDetailText");
        _sensorInventoryPanel = Find<StackPanel>("SensorInventoryPanel");
        _diagnosticsLogTextBox = Find<TextBox>("DiagnosticsLogTextBox");

        _compatibilityModelText = Find<TextBlock>("CompatibilityModelText");
        _compatibilityTypeText = Find<TextBlock>("CompatibilityTypeText");
        _compatibilityStatusChip = Find<Border>("CompatibilityStatusChip");
        _compatibilityStatusText = Find<TextBlock>("CompatibilityStatusText");
        _compatibilityMatrixPanel = Find<StackPanel>("CompatibilityMatrixPanel");
        _devModeToggle = Find<CheckBox>("DevModeToggle");

        _navBattery = Find<Button>("NavBattery");
        _navKeyboard = Find<Button>("NavKeyboard");
        _navPerformance = Find<Button>("NavPerformance");

        _guiVersionText.Text = $"v{ProjectVersion}";
        _projectVersionText.Text = $"DAMX v{ProjectVersion}";
    }

    private void BuildPageMap()
    {
        _pages["Overview"] = Find<ScrollViewer>("PageOverview");
        _pages["Performance"] = Find<ScrollViewer>("PagePerformance");
        _pages["Battery"] = Find<ScrollViewer>("PageBattery");
        _pages["Keyboard"] = Find<ScrollViewer>("PageKeyboard");
        _pages["System"] = Find<ScrollViewer>("PageSystem");
        _pages["Diagnostics"] = Find<ScrollViewer>("PageDiagnostics");
        _pages["Compatibility"] = Find<ScrollViewer>("PageCompatibility");

        foreach (var name in new[] { "NavOverview", "NavPerformance", "NavBattery", "NavKeyboard", "NavSystem",
                     "NavDiagnostics", "NavCompatibility" })
            _navButtons.Add(Find<Button>(name));
    }

    private void AttachEventHandlers()
    {
        _lowPowerProfileButton.IsCheckedChanged += ProfileButton_Checked;
        _quietProfileButton.IsCheckedChanged += ProfileButton_Checked;
        _balancedProfileButton.IsCheckedChanged += ProfileButton_Checked;
        _performanceProfileButton.IsCheckedChanged += ProfileButton_Checked;
        _turboProfileButton.IsCheckedChanged += ProfileButton_Checked;

        _autoFanSpeedRadioButton.IsCheckedChanged += (_, _) => UpdateFanModePanels();
        _maxFanSpeedRadioButton.IsCheckedChanged += (_, _) => UpdateFanModePanels();
        _manualFanSpeedRadioButton.IsCheckedChanged += (_, _) => UpdateFanModePanels();
        _curveFanRadioButton.IsCheckedChanged += (_, _) => UpdateFanModePanels();

        _autoFanSpeedRadioButton.Click += AutoFanSpeedRadioButtonClick;
        _manualFanSpeedRadioButton.Click += ManualFanControlRadioBox_Click;
        _cpuFanSlider.PropertyChanged += CpuFanSlider_ValueChanged;
        _gpuFanSlider.PropertyChanged += GpuFanSlider_ValueChanged;

        _startCalibrationButton.Click += StartCalibrationButton_Click;
        _stopCalibrationButton.Click += StopCalibrationButton_Click;

        _keyBrightnessSlider.PropertyChanged += KeyboardBrightnessSlider_ValueChanged;
        _lightingSpeedSlider.PropertyChanged += LightingSpeedSlider_ValueChanged;

        _backlightTimeoutCheckBox.Click += BacklightTimeoutCheckBox_Click;
        _lcdOverrideCheckBox.Click += LcdOverrideCheckBox_Click;
        _bootAnimAndSoundCheckBox.Click += BootSoundCheckBox_Click;

        _closeToTrayCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (_suppressToggleHandlers) return;
            ProfileStore.Preferences.CloseToTray = _closeToTrayCheckBox.IsChecked == true;
            ProfileStore.Save();
        };
        _notificationsCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (_suppressToggleHandlers) return;
            ProfileStore.Preferences.Notifications = _notificationsCheckBox.IsChecked == true;
            ToastService.Enabled = ProfileStore.Preferences.Notifications;
            ProfileStore.Save();
        };
        _devModeToggle.IsCheckedChanged += (_, _) =>
        {
            if (_suppressToggleHandlers) return;
            EnableDevMode(_devModeToggle.IsChecked == true);
        };

        ForZonePicker(_zone1ColorPicker, _zone1Border);
        ForZonePicker(_zone2ColorPicker, _zone2Border);
        ForZonePicker(_zone3ColorPicker, _zone3Border);
        ForZonePicker(_zone4ColorPicker, _zone4Border);
    }

    private void ForZonePicker(ColorPicker picker, Border preview)
    {
        picker.ColorChanged += (_, e) => preview.Background = new SolidColorBrush(e.NewColor);
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        ToastService.Host = Find<StackPanel>("ToastHost");

        _suppressToggleHandlers = true;
        _closeToTrayCheckBox.IsChecked = ProfileStore.Preferences.CloseToTray;
        _notificationsCheckBox.IsChecked = ProfileStore.Preferences.Notifications;
        ToastService.Enabled = ProfileStore.Preferences.Notifications;
        _suppressToggleHandlers = false;

        BuildProfilesList();
        BuildRgbPresets();
        BuildCurvePresets();
        ConfigureCurveForSelected();

        _powerDetection = new PowerSourceDetection();
        _isPluggedIn = _powerDetection.IsPluggedIn;
        _powerDetection.Changed += pluggedIn =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _isPluggedIn = pluggedIn;
                UpdateHeaderPower();
                UpdateUIBasedOnPowerSource();
            });
        };
        UpdateHeaderPower();

        // One shared telemetry poller for the whole app (dashboard, battery
        // page, diagnostics, fan curves). No per-timer sensor reads.
        MetricsPoller.Updated += OnMetricsUpdated;
        MetricsPoller.SetVisible(IsVisible, _curveActive);
        MetricsPoller.Start();

        SwitchPage("Overview");
        _ = InitializeAsync();
    }

    // ------------------------------------------------------------- navigation

    private void NavButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag) return;
        if (tag == "Internals")
        {
            InternalsMangerWindow_OnClick(sender, e);
            return;
        }

        SwitchPage(tag);
    }

    private void SwitchPage(string page)
    {
        if (!_pages.ContainsKey(page) || IsPageHidden(page)) page = "Overview";

        foreach (var (name, view) in _pages)
            view.IsVisible = name == page;

        foreach (var button in _navButtons)
            button.Classes.Set("active", (string?)button.Tag == page);

        (_pageTitle.Text, _pageSubtitle.Text) = page switch
        {
            "Performance" => ("Performance", "Profiles, custom presets and fan control"),
            "Battery" => ("Battery", "Charge behavior, health and calibration"),
            "Keyboard" => ("Keyboard", "Zones, presets and lighting effects"),
            "System" => ("System", "Panel, startup and application behavior"),
            "Diagnostics" => ("Diagnostics", "Daemon health, services and sensor sources"),
            "Compatibility" => ("Compatibility", "Detected hardware and feature support"),
            _ => ("Overview", "Live hardware telemetry and quick controls")
        };

        if (page == "Diagnostics") _ = RefreshDiagnosticsAsync();
        if (page == "Compatibility") BuildCompatibilityMatrix();
        if (page == "Performance") RenderCurve();
    }

    private bool IsPageHidden(string page)
    {
        return page switch
        {
            "Battery" => !_navBattery.IsVisible,
            "Keyboard" => !_navKeyboard.IsVisible,
            "Performance" => !_navPerformance.IsVisible,
            _ => false
        };
    }

    // --------------------------------------------------------------- connect

    public async Task InitializeAsync()
    {
        SetDaemonStatus("connecting");
        try
        {
            _isConnected = await _client.ConnectAsync();
            if (_isConnected)
            {
                _daemonErrorGrid.IsVisible = false;
                SetDaemonStatus("connected");
                await LoadSettingsAsync();
            }
            else
            {
                _daemonErrorGrid.IsVisible = true;
                SetDaemonStatus("error");
            }
        }
        catch (Exception ex)
        {
            _daemonErrorGrid.IsVisible = true;
            SetDaemonStatus("error");
            ToastService.Error("Initialization failed", ex.Message);
        }
    }

    private void SetDaemonStatus(string state)
    {
        var brush = (IBrush?)Application.Current?.FindResource("DmxWarning") ?? Brushes.Orange;
        switch (state)
        {
            case "connected":
                brush = (IBrush?)Application.Current?.FindResource("DmxSuccess") ?? Brushes.Green;
                _daemonStatusText.Text = "Daemon connected";
                _daemonStatusDetail.Text = "Unix socket /var/run/DAMX.sock";
                _daemonErrorGrid.IsVisible = false;
                break;
            case "error":
                brush = (IBrush?)Application.Current?.FindResource("DmxDanger") ?? Brushes.Red;
                _daemonStatusText.Text = "Daemon unavailable";
                _daemonStatusDetail.Text = "Check Diagnostics for logs";
                break;
            default:
                _daemonStatusText.Text = "Connecting…";
                _daemonStatusDetail.Text = "DAMX daemon";
                break;
        }

        _daemonStatusDot.Background = brush;
    }

    private void RetryConnectionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = InitializeAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            _settings = await _client.GetAllSettingsAsync() ?? new DAMXSettings();
        }
        catch (Exception ex)
        {
            ToastService.Error("Failed to load settings", ex.Message);
            _settings = new DAMXSettings();
        }

        ApplySettingsToUI();
    }

    private void ApplySettingsToUI()
    {
        if (_settings == null) return;

        UpdateProfileButtons();
        SetCheckBox(_backlightTimeoutCheckBox, IsEnabledSetting(_settings.BacklightTimeout));
        SetCheckBox(_batteryLimitCheckBox, IsEnabledSetting(_settings.BatteryLimiter));

        var isCalibrating = IsEnabledSetting(_settings.BatteryCalibration);
        _isCalibrating = isCalibrating;
        SetEnabled(_startCalibrationButton, !isCalibrating);
        SetEnabled(_stopCalibrationButton, isCalibrating);
        _calibrationStatusTextBlock.Text = isCalibrating ? "Status: calibrating" : "Status: not calibrating";

        SetCheckBox(_bootAnimAndSoundCheckBox, IsEnabledSetting(_settings.BootAnimationSound));
        SetCheckBox(_lcdOverrideCheckBox, IsEnabledSetting(_settings.LcdOverride));

        if (_usbChargingComboBox != null)
            _usbChargingComboBox.SelectedIndex = GetUsbChargingIndex(_settings.UsbCharging);

        var cpuSpeed = ApplyFanSpeed(_settings.FanSpeed?.Cpu, ref _cpuFanSpeed, _cpuFanSlider, _cpuFanTextBlock);
        var gpuSpeed = ApplyFanSpeed(_settings.FanSpeed?.Gpu, ref _gpuFanSpeed, _gpuFanSlider, _gpuFanTextBlock);

        var isAutoMode = cpuSpeed == 0 && gpuSpeed == 0;
        var isMaxMode = cpuSpeed == 100 && gpuSpeed == 100;
        var isManualMode = !isAutoMode && !isMaxMode;
        _isManualFanControl = isManualMode;
        _lastCurvePercent = -1;

        if (!_curveActive)
        {
            _autoFanSpeedRadioButton.IsChecked = isAutoMode;
            _maxFanSpeedRadioButton.IsChecked = isMaxMode;
            _manualFanSpeedRadioButton.IsChecked = isManualMode;
        }

        UpdateFanModePanels();
        ApplyKeyboardSettings();

        _keyBrightnessText.Text = $"{_keyboardBrightness}%";
        _lightSpeedTextBlock.Text = _lightingSpeed.ToString();
        _daemonVersionText.Text = _settings.Version;
        _driverVersionText.Text = _settings.DriverVersion;
        _laptopTypeText.Text = _settings.LaptopType;
        var features = _settings.AvailableFeatures ?? new List<string>();
        _supportedFeaturesTextBlock.Text = features.Count > 0
            ? string.Join(", ", features)
            : "None reported";
        _diagDaemonVersionText.Text = _settings.Version;
        _diagDriverVersionText.Text = _settings.DriverVersion;
        _diagLaptopText.Text = _settings.LaptopType;

        UpdateUIElementVisibility();
        UpdateDashboardConfiguration();
        BuildCompatibilityMatrix();
    }

    private void UpdateDashboardConfiguration()
    {
        _overview.Configure(_settings,
            async profile => await ApplyQuickProfileAsync(profile),
            async mode => await RequestFanModeAsync(mode));

        var mode = _curveActive ? "curve"
            : _autoFanSpeedRadioButton.IsChecked == true ? "auto"
            : _maxFanSpeedRadioButton.IsChecked == true ? "max"
            : "manual";
        _overview.SetActiveFanMode(mode);
        _overview.SetActiveProfile(_settings?.ThermalProfile?.Current ?? "balanced");
    }

    // ------------------------------------------------------- feature visibility

    private void UpdateUIElementVisibility()
    {
        if (_settings == null) return;

        _thermalProfilePanel.IsVisible = Feature("thermal_profile");
        _fanControlPanel.IsVisible = Feature("fan_speed");

        var hasBatteryFeatures = Feature("battery_calibration") || Feature("battery_limiter") ||
                                 Feature("usb_charging");
        _navBattery.IsVisible = hasBatteryFeatures;
        _calibrationControls.IsVisible = Feature("battery_calibration");
        _limiterControls.IsVisible = Feature("battery_limiter");
        _usbChargingPanel.IsVisible = Feature("usb_charging");

        var hasKeyboardFeatures = Feature("backlight_timeout") || Feature("per_zone_mode") ||
                                  Feature("four_zone_mode");
        _navKeyboard.IsVisible = hasKeyboardFeatures;
        _rgbPresetControls.IsVisible = Feature("per_zone_mode");
        _zoneColorControlPanel.IsVisible = Feature("per_zone_mode");
        _keyboardEffectsPanel.IsVisible = Feature("four_zone_mode");
        _backlightTimeoutControls.IsVisible = Feature("backlight_timeout");

        _lcdOverrideControls.IsVisible = Feature("lcd_override");
        _bootSoundControls.IsVisible = Feature("boot_animation_sound");
        _navPerformance.IsVisible = Feature("thermal_profile") || Feature("fan_speed");

        if (IsPageHidden(_pageTitle.Text ?? "Overview")) SwitchPage("Overview");
    }

    private bool Feature(string name)
    {
        return _client.IsFeatureAvailable(name) || AppState.DevMode;
    }

    private void UpdateUIBasedOnPowerSource()
    {
        UpdateProfileButtons();
        UpdateHeaderPower();
    }

    private void UpdateHeaderPower()
    {
        _headerPowerText.Text = _isPluggedIn ? "Plugged in" : "On battery";
        _headerPowerIcon.Kind = _isPluggedIn ? MaterialIconKind.PowerPlugOutline : MaterialIconKind.BatteryOutline;
        _headerPowerIcon.Foreground = _isPluggedIn
            ? (IBrush?)Application.Current?.FindResource("DmxSuccess") ?? Brushes.Green
            : (IBrush?)Application.Current?.FindResource("DmxWarning") ?? Brushes.Orange;
    }

    // ---------------------------------------------------------------- profiles

    private void UpdateProfileButtons()
    {
        if (_settings?.ThermalProfile == null) return;

        var profileConfigs =
            new Dictionary<string, (RadioButton button, string description, bool showOnBattery, bool showOnAC)>
            {
                {
                    "low-power",
                    (_lowPowerProfileButton, "Prioritizes energy efficiency to extend battery life.", true, false)
                },
                { "quiet", (_quietProfileButton, "Minimizes noise and keeps temperatures low.", false, true) },
                {
                    "balanced",
                    (_balancedProfileButton, "Optimal mix of performance and noise for everyday work.", true, true)
                },
                {
                    "balanced-performance",
                    (_performanceProfileButton, "More performance for demanding workloads.", false, true)
                },
                { "performance", (_turboProfileButton, "Maximum power, loudest fans.", false, true) }
            };

        foreach (var config in profileConfigs.Values)
        {
            config.button.IsVisible = false;
            config.button.IsEnabled = false;
        }

        foreach (var profile in _settings.ThermalProfile.Available)
        {
            var key = profile.ToLowerInvariant();
            if (!profileConfigs.TryGetValue(key, out var config)) continue;
            var show = _isPluggedIn ? config.showOnAC : config.showOnBattery;
            config.button.IsEnabled = true;
            config.button.IsVisible = show || AppState.DevMode;
        }

        if (!string.IsNullOrEmpty(_settings.ThermalProfile.Current))
        {
            var current = _settings.ThermalProfile.Current.ToLowerInvariant();
            if (profileConfigs.TryGetValue(current, out var config) && config.button.IsEnabled)
            {
                _suppressProfileSwitch = true;
                config.button.IsChecked = true;
                _suppressProfileSwitch = false;
                _thermalProfileInfoText.Text = config.description;
                _overview.SetActiveProfile(current);
            }
        }
    }

    private bool _suppressProfileSwitch;

    private async void ProfileButton_Checked(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || _suppressProfileSwitch || sender is not RadioButton button ||
            button.IsChecked != true) return;

        var profile = button.Name switch
        {
            "LowPowerProfileButton" => "low-power",
            "QuietProfileButton" => "quiet",
            "BalancedProfileButton" => "balanced",
            "PerformanceProfileButton" => "balanced-performance",
            "TurboProfileButton" => "performance",
            _ => "balanced"
        };

        await RunSafeAsync(() => ApplyProfileInternalAsync(profile, true), "Profile error");
    }

    public async Task ApplyQuickProfileAsync(string profile)
    {
        if (!_isConnected) return;
        await RunSafeAsync(() => ApplyProfileInternalAsync(profile, true), "Profile error");

        _suppressProfileSwitch = true;
        switch (profile)
        {
            case "low-power": _lowPowerProfileButton.IsChecked = true; break;
            case "quiet": _quietProfileButton.IsChecked = true; break;
            case "balanced": _balancedProfileButton.IsChecked = true; break;
            case "balanced-performance": _performanceProfileButton.IsChecked = true; break;
            case "performance": _turboProfileButton.IsChecked = true; break;
        }

        _suppressProfileSwitch = false;
    }

    private async Task ApplyProfileInternalAsync(string profile, bool notify)
    {
        await _client.SetThermalProfileAsync(profile);

        if (profile == "quiet")
        {
            await _client.SetFanSpeedAsync(0, 0);
            _isManualFanControl = false;
            if (!AppState.DevMode)
            {
                _manualDisabledInfoText.IsVisible = true;
                _manualFanSpeedRadioButton.IsEnabled = false;
                _maxFanSpeedRadioButton.IsEnabled = false;
                _curveFanRadioButton.IsEnabled = false;
                _autoFanSpeedRadioButton.IsChecked = true;
            }
        }
        else
        {
            _manualDisabledInfoText.IsVisible = false;
            _manualFanSpeedRadioButton.IsEnabled = true;
            _maxFanSpeedRadioButton.IsEnabled = true;
            _curveFanRadioButton.IsEnabled = true;
        }

        _thermalProfileInfoText.Text = profile switch
        {
            "low-power" => "Prioritizes energy efficiency to extend battery life.",
            "quiet" => "Minimizes noise and keeps temperatures low.",
            "balanced" => "Optimal mix of performance and noise for everyday work.",
            "balanced-performance" => "More performance for demanding workloads.",
            "performance" => "Maximum power, loudest fans.",
            _ => _thermalProfileInfoText.Text
        };

        _overview.SetActiveProfile(profile);
        if (notify) ToastService.Success("Profile applied", ProfileLabel(profile));

        await Task.Delay(900);
        await LoadSettingsAsync();
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
            _ => profile
        };
    }

    // --------------------------------------------------------- custom profiles

    private void BuildProfilesList()
    {
        _profilesListPanel.Children.Clear();
        var profiles = ProfileStore.Profiles.ToList();
        _profileCountText.Text = profiles.Count == 1 ? "1 saved" : $"{profiles.Count} saved";

        if (profiles.Count == 0)
        {
            _profilesListPanel.Children.Add(new TextBlock
            {
                Text = "No custom profiles yet. Configure the machine, then save the current state.",
                Classes = { "Dim" },
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var profile in profiles)
        {
            var name = new TextBlock
            {
                Text = profile.Name,
                FontFamily = Application.Current?.FindResource("OpenSansSemiBoldFont") as FontFamily ??
                             FontFamily.Default,
                FontSize = 13
            };
            var summary = new TextBlock
            {
                Text = ProfileSummary(profile),
                Classes = { "Caption" }
            };

            var info = new StackPanel { Spacing = 2 };
            info.Children.Add(name);
            info.Children.Add(summary);

            var apply = new Button { Content = "Apply", Classes = { "Ghost" } };
            apply.Click += async (_, _) => await ApplyCustomProfileAsync(profile, true);

            var delete = new Button { Classes = { "Icon" }, Margin = new Thickness(6, 0, 0, 0) };
            delete.Content = new Material.Icons.Avalonia.MaterialIcon
            {
                Kind = MaterialIconKind.DeleteOutline,
                Width = 15,
                Height = 15
            };
            delete.Click += (_, _) =>
            {
                ProfileStore.DeleteProfile(profile.Name);
                BuildProfilesList();
                ToastService.Show("Profile deleted", profile.Name);
            };

            var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            actions.Children.Add(apply);
            actions.Children.Add(delete);

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(info);
            Grid.SetColumn(actions, 1);
            row.Children.Add(actions);

            var border = new Border
            {
                Classes = { "Well" },
                Child = row
            };
            _profilesListPanel.Children.Add(border);
        }
    }

    private static string ProfileSummary(CustomProfile profile)
    {
        var fan = profile.FanMode switch
        {
            "max" => "max fans",
            "manual" => $"fans {profile.CpuFan}/{profile.GpuFan}%",
            _ => "auto fans"
        };
        var bits = new List<string> { ProfileLabel(profile.Thermal), fan };
        if (profile.BatteryLimit) bits.Add("80% charge limit");
        return string.Join(" · ", bits);
    }

    private void SaveProfileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var name = _profileNameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ToastService.Warning("Name required", "Give the profile a name before saving.");
            return;
        }

        var fanMode = _curveActive ? "auto"
            : _maxFanSpeedRadioButton.IsChecked == true ? "max"
            : _manualFanSpeedRadioButton.IsChecked == true ? "manual"
            : "auto";

        var profile = new CustomProfile
        {
            Name = name,
            Thermal = _settings?.ThermalProfile?.Current ?? "balanced",
            CpuFan = _cpuFanSpeed,
            GpuFan = _gpuFanSpeed,
            FanMode = fanMode,
            BatteryLimit = _batteryLimitCheckBox.IsChecked == true,
            UsbCharging = UsbIndexToLevel(_usbChargingComboBox.SelectedIndex)
        };

        ProfileStore.AddOrUpdateProfile(profile);
        _profileNameTextBox.Text = "";
        BuildProfilesList();
        ToastService.Success("Profile saved", $"{name} · {ProfileSummary(profile)}");
    }

    private async Task ApplyCustomProfileAsync(CustomProfile profile, bool notify)
    {
        if (!_isConnected)
        {
            ToastService.Error("Daemon offline", "Cannot apply a profile without the daemon.");
            return;
        }

        try
        {
            if (_settings?.ThermalProfile?.Available.Contains(profile.Thermal) == true)
                await _client.SetThermalProfileAsync(profile.Thermal);

            switch (profile.FanMode)
            {
                case "max":
                    await _client.SetFanSpeedAsync(100, 100);
                    break;
                case "manual":
                    await _client.SetFanSpeedAsync(profile.CpuFan, profile.GpuFan);
                    break;
                default:
                    await _client.SetFanSpeedAsync(0, 0);
                    break;
            }

            await _client.SetBatteryLimiterAsync(profile.BatteryLimit);
            await _client.SetUsbChargingAsync(profile.UsbCharging);

            if (notify) ToastService.Success("Custom profile applied", profile.Name);
            await Task.Delay(800);
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error("Could not apply profile", ex.Message);
        }
    }

    // ---------------------------------------------------------------- fans

    private void UpdateFanModePanels()
    {
        var manual = _manualFanSpeedRadioButton.IsChecked == true;
        var curve = _curveFanRadioButton.IsChecked == true;
        _manualFanPanel.IsVisible = manual;
        _curveFanPanel.IsVisible = curve;

        if (curve && !_curveActive) EnableCurve();
        if (!curve && _curveActive) DisableCurve();

        var mode = curve ? "curve"
            : _maxFanSpeedRadioButton.IsChecked == true ? "max"
            : manual ? "manual"
            : "auto";
        _overview.SetActiveFanMode(mode);
    }

    private void ManualFanControlRadioBox_Click(object? sender, RoutedEventArgs e)
    {
        _isManualFanControl = true;
        _manualFanSpeedRadioButton.IsChecked = true;
    }

    private void CpuFanSlider_ValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Slider.ValueProperty) return;
        _cpuFanSpeed = Convert.ToInt32(e.NewValue);
        _cpuFanTextBlock.Text = _cpuFanSpeed == 0 ? "Auto" : $"{_cpuFanSpeed}%";
    }

    private void GpuFanSlider_ValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Slider.ValueProperty) return;
        _gpuFanSpeed = Convert.ToInt32(e.NewValue);
        _gpuFanTextBlock.Text = _gpuFanSpeed == 0 ? "Auto" : $"{_gpuFanSpeed}%";
    }

    private async void SetManualSpeedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || _isSettingFanSpeed) return;
        _isSettingFanSpeed = true;
        _setManualSpeedButton.IsEnabled = false;
        try
        {
            await _client.SetFanSpeedAsync(_cpuFanSpeed, _gpuFanSpeed);
            ToastService.Success("Fan speed applied", $"CPU {_cpuFanSpeed}% · GPU {_gpuFanSpeed}%");
        }
        catch (Exception ex)
        {
            ToastService.Error("Fan speed error", ex.Message);
        }
        finally
        {
            _isSettingFanSpeed = false;
            _setManualSpeedButton.IsEnabled = true;
        }
    }

    private async void AutoFanSpeedRadioButtonClick(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected) return;
        await RunSafeAsync(async () =>
        {
            await _client.SetFanSpeedAsync(0, 0);
            _isManualFanControl = false;
            _manualFanSpeedRadioButton.IsChecked = false;
            ToastService.Show("Cooling", "Automatic fan control enabled");
            await LoadSettingsAsync();
        }, "Cooling error");
    }

    private async void MaxFanSpeedRadioButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected) return;
        _cpuFanSpeed = 100;
        _gpuFanSpeed = 100;
        _isManualFanControl = false;
        _cpuFanSlider.Value = 100;
        _gpuFanSlider.Value = 100;
        _cpuFanTextBlock.Text = "100%";
        _gpuFanTextBlock.Text = "100%";
        await RunSafeAsync(async () =>
        {
            await _client.SetFanSpeedAsync(100, 100);
            ToastService.Success("Cooling", "Fans set to maximum");
        }, "Cooling error");
    }

    // ---------------------------------------------------------------- curves

    public async Task RequestFanModeAsync(string mode)
    {
        switch (mode)
        {
            case "max":
                _maxFanSpeedRadioButton.IsChecked = true;
                await RunSafeAsync(async () =>
                {
                    await _client.SetFanSpeedAsync(100, 100);
                    ToastService.Success("Cooling", "Fans set to maximum");
                }, "Cooling error");
                break;
            case "manual":
                _manualFanSpeedRadioButton.IsChecked = true;
                SwitchPage("Performance");
                break;
            case "auto":
            default:
                _autoFanSpeedRadioButton.IsChecked = true;
                await RunSafeAsync(async () =>
                {
                    await _client.SetFanSpeedAsync(0, 0);
                    ToastService.Show("Cooling", "Automatic fan control enabled");
                }, "Cooling error");
                break;
        }
    }

    private void ConfigureCurveForSelected()
    {
        var name = ProfileStore.Preferences.ActiveCurve ?? "Balanced";
        _editingCurve = CloneCurve(ProfileStore.GetCurve(name) ?? ProfileStore.Curves.First());
        _editingCurve.Name = ProfileStore.GetCurve(_editingCurve.Name)?.Name ?? "Custom";
        BuildCurvePointsEditor();
        RenderCurve();
        MarkActiveCurveChip();
    }

    private static FanCurveDefinition CloneCurve(FanCurveDefinition source)
    {
        return new FanCurveDefinition
        {
            Name = source.Name,
            Points = source.Points.Select(p => new FanCurvePoint { Temp = p.Temp, Percent = p.Percent }).ToList()
        };
    }

    private void BuildCurvePresets()
    {
        _curvePresetPanel.Children.Clear();
        foreach (var curve in ProfileStore.Curves)
        {
            var chip = new Button { Content = curve.Name, Classes = { "Chip" }, Tag = curve.Name };
            chip.Click += (_, _) => ApplyCurvePreset(curve.Name);
            _curvePresetPanel.Children.Add(chip);
        }
    }

    private void MarkActiveCurveChip()
    {
        foreach (var chip in _curvePresetPanel.Children.OfType<Button>())
            chip.Classes.Set("active", (string?)chip.Tag == _editingCurve.Name);
    }

    private void ApplyCurvePreset(string name)
    {
        var curve = ProfileStore.GetCurve(name);
        if (curve == null) return;

        _editingCurve = CloneCurve(curve);
        ProfileStore.Preferences.ActiveCurve = name;
        ProfileStore.Save();
        BuildCurvePointsEditor();
        RenderCurve();
        MarkActiveCurveChip();
        _lastCurvePercent = -1;
        ToastService.Show("Fan curve loaded", name);
    }

    private void EnableCurve()
    {
        _curveActive = true;
        MetricsPoller.SetVisible(IsVisible, _curveActive);
        _curveStatusText.Text = "Curve active — controlled by DAMX while the app is running.";
        if (MetricsPoller.Latest is { } latest) ApplyCurveOnUi(latest);
    }

    private void DisableCurve()
    {
        _curveActive = false;
        MetricsPoller.SetVisible(IsVisible, _curveActive);
        _lastCurvePercent = -1;
        _curveStatusText.Text = "Curve applies while DAMX is running.";
        if (_isConnected) _ = RunSafeAsync(() => _client.SetFanSpeedAsync(0, 0), "Fan curve error");
    }

    /// <summary>Evaluate the active curve from the shared snapshot (UI thread).</summary>
    private void ApplyCurveOnUi(SystemSnapshot snap)
    {
        if (!_curveActive || !_isConnected) return;

        var temp = Math.Max(snap.CpuTemp, snap.GpuTemp);
        if (temp <= 0) temp = snap.CpuTemp;
        if (temp <= 0) return;

        var percent = ProfileStore.EvaluateCurve(_editingCurve, temp);
        if (Math.Abs(percent - _lastCurvePercent) < 3) return;

        _lastCurvePercent = percent;
        _ = RunSafeAsync(() => _client.SetFanSpeedAsync(percent, percent), "Fan curve error");
        _curveStatusText.Text = $"Curve active — {temp:F0} °C → {percent}% fans.";
    }

    private void BuildCurvePointsEditor()
    {
        _curvePointsPanel.Children.Clear();

        foreach (var point in _editingCurve.Points.OrderBy(p => p.Temp).ToList())
        {
            var tempText = new TextBlock
            {
                Text = $"{point.Temp:F0} °C",
                Width = 56,
                Classes = { "Value" },
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var fanText = new TextBlock
            {
                Text = $"{point.Percent}%",
                Width = 44,
                Classes = { "Value" },
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var tempSlider = new Slider
            {
                Minimum = 30,
                Maximum = 100,
                Value = point.Temp,
                Width = 200,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var fanSlider = new Slider
            {
                Minimum = 10,
                Maximum = 100,
                Value = point.Percent,
                Width = 200,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            tempSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property != Slider.ValueProperty) return;
                point.Temp = Math.Round(Convert.ToDouble(e.NewValue));
                tempText.Text = $"{point.Temp:F0} °C";
                RenderCurve();
            };
            fanSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property != Slider.ValueProperty) return;
                point.Percent = (int)Math.Round(Convert.ToDouble(e.NewValue));
                fanText.Text = $"{point.Percent}%";
                RenderCurve();
            };

            var remove = new Button { Classes = { "Icon" } };
            remove.Content = new Material.Icons.Avalonia.MaterialIcon
            {
                Kind = MaterialIconKind.Close,
                Width = 14,
                Height = 14
            };
            remove.IsEnabled = _editingCurve.Points.Count > 2;
            remove.Click += (_, _) =>
            {
                _editingCurve.Points.Remove(point);
                BuildCurvePointsEditor();
                RenderCurve();
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto"),
                ColumnSpacing = 10
            };
            row.Children.Add(new TextBlock
            {
                Text = "Temp",
                Classes = { "Label" },
                Width = 40,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            row.Children.Add(tempText);
            row.Children.Add(tempSlider);
            row.Children.Add(fanText);
            row.Children.Add(fanSlider);
            Grid.SetColumn(tempText, 1);
            Grid.SetColumn(tempSlider, 2);
            Grid.SetColumn(fanText, 3);
            Grid.SetColumn(fanSlider, 4);

            var outer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            outer.Children.Add(row);
            Grid.SetColumn(remove, 1);
            outer.Children.Add(remove);

            _curvePointsPanel.Children.Add(new Border { Classes = { "Well" }, Child = outer });
        }
    }

    private void AddCurvePointButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var points = _editingCurve.Points.OrderBy(p => p.Temp).ToList();
        double nextTemp;

        if (points.Count >= 2)
        {
            // Find the widest gap and insert in the middle.
            var bestGap = 0.0;
            nextTemp = 75;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var gap = points[i + 1].Temp - points[i].Temp;
                if (gap <= bestGap) continue;
                bestGap = gap;
                nextTemp = Math.Round(points[i].Temp + gap / 2);
            }
        }
        else
        {
            nextTemp = points.Count == 1 ? Math.Min(100, points[0].Temp + 15) : 60;
        }

        var percent = ProfileStore.EvaluateCurve(_editingCurve, nextTemp);
        _editingCurve.Points.Add(new FanCurvePoint { Temp = nextTemp, Percent = percent });
        BuildCurvePointsEditor();
        RenderCurve();
    }

    private void SaveCurveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _editingCurve.Points = _editingCurve.Points.OrderBy(p => p.Temp).ToList();
        ProfileStore.SaveCurve(_editingCurve);
        ProfileStore.Preferences.ActiveCurve = _editingCurve.Name;
        ProfileStore.Save();
        BuildCurvePresets();
        MarkActiveCurveChip();
        _lastCurvePercent = -1;
        ToastService.Success("Fan curve saved", $"{_editingCurve.Name} · {_editingCurve.Points.Count} points");
    }

    private void RenderCurve()
    {
        if (_curveCanvas == null) return;

        _curveCanvas.Children.Clear();
        var width = _curveCanvas.Bounds.Width > 80 ? _curveCanvas.Bounds.Width : 560;
        const double height = 190;
        const double left = 6;
        var plotWidth = width - 12;

        var gridBrush = new SolidColorBrush(Color.Parse("#23232D"));
        var accent = (IBrush?)Application.Current?.FindResource("DmxAccent") ?? Brushes.DeepSkyBlue;

        for (var i = 0; i <= 4; i++)
        {
            var y = 10 + i * (height - 30) / 4.0;
            _curveCanvas.Children.Add(new Shapes.Line
            {
                StartPoint = new Point(left, y),
                EndPoint = new Point(left + plotWidth, y),
                Stroke = gridBrush,
                StrokeThickness = 1
            });
        }

        Point Map(FanCurvePoint p)
        {
            var x = left + Math.Clamp((p.Temp - 30) / 70.0, 0, 1) * plotWidth;
            var y = height - 20 - Math.Clamp(p.Percent / 100.0, 0, 1) * (height - 30);
            return new Point(x, y);
        }

        var ordered = _editingCurve.Points.OrderBy(p => p.Temp).ToList();
        if (ordered.Count >= 2)
        {
            var polyline = new Shapes.Polyline
            {
                Stroke = accent,
                StrokeThickness = 2,
                Points = ordered.Select(Map).ToList()
            };
            _curveCanvas.Children.Add(polyline);
        }

        foreach (var point in ordered)
        {
            var dot = new Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = accent,
                Stroke = (IBrush?)Application.Current?.FindResource("DmxSurface") ?? Brushes.Black,
                StrokeThickness = 2
            };
            var pos = Map(point);
            Canvas.SetLeft(dot, pos.X - 4.5);
            Canvas.SetTop(dot, pos.Y - 4.5);
            _curveCanvas.Children.Add(dot);
        }

        AddCanvasLabel("30 °C", left, height - 16);
        AddCanvasLabel("100 °C", left + plotWidth - 44, height - 16);
        AddCanvasLabel("100 %", left + plotWidth - 40, 0);
    }

    private void AddCanvasLabel(string text, double x, double y)
    {
        var label = new TextBlock { Text = text, Classes = { "Caption" } };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        _curveCanvas.Children.Add(label);
    }

    // ---------------------------------------------------------------- battery

    private async void StartCalibrationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected) return;
        await RunSafeAsync(async () =>
        {
            await _client.SetBatteryCalibrationAsync(true);
            _isCalibrating = true;
            _startCalibrationButton.IsEnabled = false;
            _stopCalibrationButton.IsEnabled = true;
            _calibrationStatusTextBlock.Text = "Status: calibrating";
            ToastService.Success("Calibration started", "Keep the charger connected for the full cycle.");
        }, "Calibration error");
    }

    private async void StopCalibrationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected) return;
        await RunSafeAsync(async () =>
        {
            await _client.SetBatteryCalibrationAsync(false);
            _isCalibrating = false;
            _startCalibrationButton.IsEnabled = true;
            _stopCalibrationButton.IsEnabled = false;
            _calibrationStatusTextBlock.Text = "Status: not calibrating";
            ToastService.Show("Calibration stopped", "Battery calibration was cancelled.");
        }, "Calibration error");
    }

    private async void BatteryLimitCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || sender is not CheckBox checkBox) return;
        var enabled = checkBox.IsChecked ?? false;
        await RunSafeAsync(async () =>
        {
            await _client.SetBatteryLimiterAsync(enabled);
            ToastService.Show("Charge limiter", enabled ? "Charging limited to 80 %" : "Full charging enabled");
        }, "Battery limiter error");
    }

    private async void UsbChargingComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isConnected || _usbChargingComboBox == null) return;
        await RunSafeAsync(() => _client.SetUsbChargingAsync(UsbIndexToLevel(_usbChargingComboBox.SelectedIndex)),
            "USB charging error");
    }

    // ---------------------------------------------------------------- keyboard

    private sealed record RgbPreset(string Name, string Z1, string Z2, string Z3, string Z4);

    private static readonly RgbPreset[] RgbPresets =
    {
        new("Nitro lime", "7AC943", "7AC943", "7AC943", "7AC943"),
        new("Predator red", "E11D2E", "E11D2E", "E11D2E", "E11D2E"),
        new("Ice blue", "3D9BF7", "3D9BF7", "3D9BF7", "3D9BF7"),
        new("Warm white", "F5E6C8", "F5E6C8", "F5E6C8", "F5E6C8"),
        new("Aurora", "3D9BF7", "38C793", "7C5CFF", "E9A83C"),
        new("Sunset", "F0595E", "E9A83C", "7C5CFF", "3D9BF7"),
        new("Rainbow", "F0595E", "E9A83C", "38C793", "3D9BF7"),
        new("Off", "000000", "000000", "000000", "000000")
    };

    private void BuildRgbPresets()
    {
        _rgbPresetsPanel.Children.Clear();
        foreach (var preset in RgbPresets)
        {
            var swatch = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.Parse(preset.Z2 == "000000" ? "#404040" : $"#{preset.Z2}"))
            };
            var content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(swatch);
            content.Children.Add(new TextBlock { Text = preset.Name, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });

            var chip = new Button { Content = content, Classes = { "Chip" } };
            chip.Click += async (_, _) => await ApplyRgbPresetAsync(preset);
            _rgbPresetsPanel.Children.Add(chip);
        }
    }

    private async Task ApplyRgbPresetAsync(RgbPreset preset)
    {
        if (!_isConnected || _settings?.HasFourZoneKb != true) return;

        SetColorPicker(_zone1ColorPicker, preset.Z1);
        SetColorPicker(_zone2ColorPicker, preset.Z2);
        SetColorPicker(_zone3ColorPicker, preset.Z3);
        SetColorPicker(_zone4ColorPicker, preset.Z4);
        UpdateZonePreviews();

        await _client.SetPerZoneModeAsync(preset.Z1, preset.Z2, preset.Z3, preset.Z4, _keyboardBrightness);
        SaveZonePresetFromUI();
        ToastService.Success("Lighting preset applied", preset.Name);
    }

    private void ApplyKeyboardSettings()
    {
        if (!_settings!.HasFourZoneKb) return;

        ApplySavedZonePresetToUI();
        if (!File.Exists(KeyboardZonePresetPath)) ApplyPerZoneSettingsToUI();
        ApplyFourZoneSettingsToUI();
        ApplySavedLightingEffectPresetToUI();
        UpdateZonePreviews();
    }

    private void UpdateZonePreviews()
    {
        _zone1Border.Background = new SolidColorBrush(_zone1ColorPicker.Color);
        _zone2Border.Background = new SolidColorBrush(_zone2ColorPicker.Color);
        _zone3Border.Background = new SolidColorBrush(_zone3ColorPicker.Color);
        _zone4Border.Background = new SolidColorBrush(_zone4ColorPicker.Color);
    }

    private void ApplyPerZoneSettingsToUI()
    {
        if (!TryParsePerZoneMode(_settings!.PerZoneMode, out var z1, out var z2, out var z3, out var z4,
                out var brightness)) return;

        SetColorPicker(_zone1ColorPicker, z1);
        SetColorPicker(_zone2ColorPicker, z2);
        SetColorPicker(_zone3ColorPicker, z3);
        SetColorPicker(_zone4ColorPicker, z4);
        SetKeyboardBrightness(brightness);
    }

    private void ApplyFourZoneSettingsToUI()
    {
        if (!TryParseFourZoneMode(_settings!.FourZoneMode, out var mode, out var speed, out var brightness,
                out var direction, out var red, out var green, out var blue)) return;

        _lightingModeComboBox.SelectedIndex = mode;
        SetLightingSpeed(speed);
        SetKeyboardBrightness(brightness);
        SetDirectionRadioButtons(direction);
        if (mode != 2 || red != 0 || green != 0 || blue != 0)
            SetColorPicker(_lightEffectColorPicker, $"{red:X2}{green:X2}{blue:X2}");
    }

    private void KeyboardBrightnessSlider_ValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.ValueProperty) SetKeyboardBrightness(Convert.ToInt32(e.NewValue), false);
    }

    private async void ApplyKeyboardColorsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || _settings?.HasFourZoneKb != true) return;
        await RunSafeAsync(async () =>
        {
            await _client.SetPerZoneModeAsync(
                ToRgbHex(_zone1ColorPicker.Color),
                ToRgbHex(_zone2ColorPicker.Color),
                ToRgbHex(_zone3ColorPicker.Color),
                ToRgbHex(_zone4ColorPicker.Color),
                _keyboardBrightness);
            SaveZonePresetFromUI();
            ToastService.Success("Zone colors applied", $"{_keyboardBrightness}% brightness");
        }, "Keyboard error");
    }

    private void LightingSpeedSlider_ValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.ValueProperty) SetLightingSpeed(Convert.ToInt32(e.NewValue), false);
    }

    private async void LightingEffectsApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || _settings?.HasFourZoneKb != true) return;

        var mode = _lightingModeComboBox?.SelectedIndex ?? 0;
        var direction = GetSelectedDirection();
        var color = _lightEffectColorPicker?.Color ?? Color.Parse(DefaultEffectColor);

        await RunSafeAsync(async () =>
        {
            if (mode == 0)
            {
                var rgb = ToRgbHex(color);
                await _client.SetPerZoneModeAsync(rgb, rgb, rgb, rgb, _keyboardBrightness);
                SaveLightingEffectPresetFromUI(mode, direction, color);
                ToastService.Success("Lighting applied", "Static color");
                return;
            }

            await _client.SetFourZoneModeAsync(mode, _lightingSpeed, _keyboardBrightness, direction,
                color.R, color.G, color.B);
            SaveLightingEffectPresetFromUI(mode, direction, color);
            ToastService.Success("Effect applied", _lightingModeComboBox?.SelectedItem?.ToString() ?? "Effect");
        }, "Lighting error");
    }

    private async void BacklightTimeoutCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || sender is not CheckBox checkBox) return;
        await RunSafeAsync(async () =>
        {
            await _client.SetBacklightTimeoutAsync(checkBox.IsChecked ?? false);
            ToastService.Show("Backlight timeout", checkBox.IsChecked == true ? "Enabled (30 s)" : "Disabled");
        }, "Keyboard error");
    }

    private async void LcdOverrideCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || sender is not CheckBox checkBox) return;
        await RunSafeAsync(async () =>
        {
            await _client.SetLcdOverrideAsync(checkBox.IsChecked ?? false);
            ToastService.Show("LCD override", checkBox.IsChecked == true ? "Enabled" : "Disabled");
        }, "Display error");
    }

    private async void BootSoundCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isConnected || sender is not CheckBox checkBox) return;
        await RunSafeAsync(async () =>
        {
            await _client.SetBootAnimationSoundAsync(checkBox.IsChecked ?? false);
            ToastService.Show("Startup sound", checkBox.IsChecked == true ? "Enabled" : "Disabled");
        }, "Startup error");
    }

    // ------------------------------------------------------------- system info

    private bool _staticInfoApplied;

    /// <summary>Shared telemetry callback (thread-pool thread).</summary>
    private void OnMetricsUpdated(SystemSnapshot snap)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_staticInfoApplied)
            {
                _staticInfoApplied = true;
                _modelNameText.Text = snap.Model;
                _osNameText.Text = snap.OsVersion;
                _kernelText.Text = snap.KernelVersion;
                _compatibilityModelText.Text = snap.Model;
                _compatibilityTypeText.Text = _settings?.LaptopType ?? "UNKNOWN";
            }

            if (snap.HasBattery)
            {
                _batteryPagePercent.Text = $"{snap.BatteryPercent} %";
                _batteryPageBar.Value = snap.BatteryPercent;
                _batteryPageStatus.Text = snap.IsPluggedIn
                    ? $"{snap.BatteryStatus} · plugged in"
                    : $"{snap.BatteryStatus} · on battery";
                _batteryPageTime.Text = snap.BatteryTimeRemaining;
                _batteryHealthText.Text = snap.BatteryHealthPct.HasValue ? $"{snap.BatteryHealthPct:F1} %" : "—";
                _batteryHealthBar.Value = snap.BatteryHealthPct ?? 0;
                _batteryCyclesText.Text = snap.BatteryCycles?.ToString() ?? "—";
                _batteryEnergyText.Text = snap.BatteryEnergyWh.HasValue && snap.BatteryDesignWh.HasValue
                    ? $"{snap.BatteryEnergyWh:F1} / {snap.BatteryDesignWh:F1} Wh"
                    : "—";
                _batteryPowerText.Text = snap.BatteryPowerW.HasValue ? $"{snap.BatteryPowerW:F1} W" : "—";
            }

            if (_pages["Diagnostics"].IsVisible) PopulateSensorInventory(snap);
            if (_curveActive) ApplyCurveOnUi(snap);
        });
    }

    // ------------------------------------------------------------- diagnostics

    private async Task RefreshDiagnosticsAsync()
    {
        _diagConnectionChipText.Text = _isConnected ? "Connected" : "Disconnected";
        _diagConnectionChipText.Foreground = _isConnected
            ? (IBrush?)Application.Current?.FindResource("DmxSuccess") ?? Brushes.Green
            : (IBrush?)Application.Current?.FindResource("DmxDanger") ?? Brushes.Red;
        _diagConnectionText.Text = _isConnected
            ? "Unix socket connection established."
            : "Not connected. Start the daemon or retry from the sidebar.";

        _diagDaemonVersionText.Text = _settings?.Version ?? "unknown";
        _diagDriverVersionText.Text = _settings?.DriverVersion ?? "unknown";
        _diagLaptopText.Text = _settings?.LaptopType ?? "unknown";

        try
        {
            var services = await DiagnosticsService.GetCoreServicesAsync();
            _diagServicesPanel.Children.Clear();
            foreach (var service in services) _diagServicesPanel.Children.Add(BuildServiceRow(service));
        }
        catch
        {
            // Leave the previous list in place.
        }

        var snap = MetricsPoller.Latest ?? await MetricsPoller.RefreshNowAsync();
        PopulateSensorInventory(snap);
        BuildNitroStatus(services: null);

        _diagnosticsLogTextBox.Text = DiagnosticsService.TailDaemonLog(120);
    }

    private static Control BuildServiceRow(ServiceState service)
    {
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        dot.Background = service.Active == "active"
            ? (IBrush?)Application.Current?.FindResource("DmxSuccess") ?? Brushes.Green
            : (IBrush?)Application.Current?.FindResource("DmxTextMuted") ?? Brushes.Gray;

        var info = new StackPanel { Spacing = 1 };
        info.Children.Add(new TextBlock { Text = service.Label, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
        info.Children.Add(new TextBlock
        {
            Text = $"{service.Unit} · {service.Active} · {service.Enabled}",
            Classes = { "Caption" }
        });

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
        row.Children.Add(dot);
        Grid.SetColumn(info, 1);
        row.Children.Add(info);
        return new Border { Classes = { "Well" }, Child = row };
    }

    private void PopulateSensorInventory(SystemSnapshot snap)
    {
        _sensorInventoryPanel.Children.Clear();
        AddSensorRow("CPU temperature", snap.CpuTempSource);
        AddSensorRow("GPU metrics", snap.GpuSource);
        AddSensorRow("Fans", snap.FanSource);
        AddSensorRow("Battery", snap.BatterySource);
    }

    private void AddSensorRow(string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*") };
        row.Children.Add(new TextBlock { Text = label, Classes = { "Label" } });
        var valueText = new TextBlock
        {
            Text = value,
            Classes = { "Value" },
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);
        _sensorInventoryPanel.Children.Add(row);
    }

    private async void BuildNitroStatus(ServiceState[]? services = null)
    {
        try
        {
            services ??= await DiagnosticsService.GetCoreServicesAsync();
            var nitro = services.FirstOrDefault(s => s.Unit.StartsWith("nitro"));
            var remapper = DiagnosticsService.IsKeyRemapperRunning();

            if (nitro?.Active == "active")
            {
                _nitroButtonStatusText.Text = "Detection service running";
                _nitroButtonStatusText.Foreground =
                    (IBrush?)Application.Current?.FindResource("DmxSuccess") ?? Brushes.Green;
                _nitroButtonDetailText.Text =
                    "Press the N / PredatorSense key while DAMX is closed to launch it.";
            }
            else if (remapper)
            {
                _nitroButtonStatusText.Text = "Key remapper detected";
                _nitroButtonStatusText.Foreground =
                    (IBrush?)Application.Current?.FindResource("DmxWarning") ?? Brushes.Orange;
                _nitroButtonDetailText.Text =
                    "keyd or kmonad owns the keyboard, so the detection service cannot read the button. Bind the key in the remapper config instead.";
            }
            else
            {
                _nitroButtonStatusText.Text = "Detection service not active";
                _nitroButtonStatusText.Foreground =
                    (IBrush?)Application.Current?.FindResource("DmxTextSecondary") ?? Brushes.Gray;
                _nitroButtonDetailText.Text =
                    "Run the DAMX installer with the Nitro-key option to enable button launch support.";
            }
        }
        catch
        {
            _nitroButtonStatusText.Text = "Status unavailable";
        }
    }

    private async void RefreshDiagnosticsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RefreshDiagnosticsAsync();
        ToastService.Show("Diagnostics", "Refreshed");
    }

    private async void CopyDiagnosticsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var snap = MetricsPoller.Latest ?? await MetricsPoller.RefreshNowAsync();
            var services = await DiagnosticsService.GetCoreServicesAsync();
            var report = DiagnosticsService.BuildReport(ProjectVersion, _settings, snap, services);
            var clipboard = Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(report);
            ToastService.Success("Report copied", "Diagnostics report is on the clipboard.");
        }
        catch (Exception ex)
        {
            ToastService.Error("Copy failed", ex.Message);
        }
    }

    private async void RestartDaemonButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_client.IsConnected) return;
        ToastService.Show("Restarting daemon", "The GUI will reconnect automatically.");
        await RunSafeAsync(async () =>
        {
            await _client.SendCommandAsync("restart_daemon");
            await Task.Delay(1500);
            await InitializeAsync();
        }, "Restart failed");
    }

    private async void RestartSuiteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_client.IsConnected) return;
        ToastService.Warning("Restarting drivers + daemon", "All parameters are cleared on restart.");
        await RunSafeAsync(async () =>
        {
            await _client.SendCommandAsync("restart_drivers_and_daemon");
            await Task.Delay(1800);
            MetricsService.Refresh();
            await InitializeAsync();
        }, "Restart failed");
    }

    private void OpenLogsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl(DiagnosticsService.DaemonLogPath);
    }

    // ----------------------------------------------------------- compatibility

    private sealed record FeatureDefinition(string Key, string Name, string Description, string Category);

    private static readonly FeatureDefinition[] KnownFeatures =
    {
        new("thermal_profile", "Performance profiles", "Eco, Quiet, Balanced, Performance and Turbo modes", "Power"),
        new("fan_speed", "Fan control", "Automatic, maximum and manual CPU / GPU fan speeds", "Cooling"),
        new("battery_limiter", "Charge limiter", "Caps charging at 80 % to extend battery lifespan", "Battery"),
        new("battery_calibration", "Battery calibration", "Full charge–drain–recharge calibration cycle", "Battery"),
        new("usb_charging", "USB Power Delivery", "USB charging while the laptop is powered off", "Battery"),
        new("per_zone_mode", "Per-zone RGB", "Static colors for the four keyboard zones", "Keyboard"),
        new("four_zone_mode", "RGB effects", "Breathing, wave, meteor and other animated modes", "Keyboard"),
        new("backlight_timeout", "Backlight timeout", "Turns keyboard lighting off after inactivity", "Keyboard"),
        new("lcd_override", "LCD override", "Reduces panel latency and ghosting", "Display"),
        new("boot_animation_sound", "Boot animation & sound", "Acer startup animation and chime", "Startup")
    };

    private void BuildCompatibilityMatrix()
    {
        if (_compatibilityMatrixPanel == null) return;

        var features = _settings?.AvailableFeatures ?? new List<string>();
        var supported = features.Count;

        _compatibilityModelText.Text = _settings?.LaptopType == null || _settings.LaptopType == "UNKNOWN"
            ? _modelNameText.Text
            : _modelNameText.Text;
        _compatibilityTypeText.Text = _settings?.LaptopType ?? "UNKNOWN";

        _compatibilityStatusText.Text = features.Count == 0
            ? "No features reported yet. Make sure the daemon is connected and the drivers are loaded."
            : $"{supported} of {KnownFeatures.Length} features supported on this machine.";

        _compatibilityStatusChip.Background = supported > 0
            ? (IBrush?)Application.Current?.FindResource("DmxSuccessSoft") ?? Brushes.DarkGreen
            : (IBrush?)Application.Current?.FindResource("DmxWarningSoft") ?? Brushes.DarkOrange;
        _compatibilityStatusChip.BorderBrush = supported > 0
            ? (IBrush?)Application.Current?.FindResource("DmxSuccess") ?? Brushes.Green
            : (IBrush?)Application.Current?.FindResource("DmxWarning") ?? Brushes.Orange;

        _compatibilityMatrixPanel.Children.Clear();
        foreach (var definition in KnownFeatures)
            _compatibilityMatrixPanel.Children.Add(BuildFeatureRow(definition, features.Contains(definition.Key)));
    }

    private Control BuildFeatureRow(FeatureDefinition definition, bool supported)
    {
        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = definition.Name,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold
        });
        info.Children.Add(new TextBlock
        {
            Text = definition.Description,
            Classes = { "Caption" },
            TextWrapping = TextWrapping.Wrap
        });

        var chip = new Border
        {
            Padding = new Thickness(10, 4),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Background = supported
                ? (IBrush?)Application.Current?.FindResource("DmxSuccessSoft") ?? Brushes.DarkGreen
                : (IBrush?)Application.Current?.FindResource("DmxWell") ?? Brushes.Black
        };
        chip.Child = new TextBlock
        {
            Text = supported ? "Supported" : Feature(definition.Key) ? "Forced" : "Not detected",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = supported
                ? (IBrush?)Application.Current?.FindResource("DmxSuccess") ?? Brushes.Green
                : Feature(definition.Key)
                    ? (IBrush?)Application.Current?.FindResource("DmxWarning") ?? Brushes.Orange
                    : (IBrush?)Application.Current?.FindResource("DmxTextMuted") ?? Brushes.Gray
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        row.Children.Add(info);
        Grid.SetColumn(chip, 1);
        row.Children.Add(chip);
        return new Border { Classes = { "Well" }, Child = row };
    }

    // ------------------------------------------------------------- keyboard IO

    private void SetKeyboardBrightness(int brightness, bool updateSlider = true)
    {
        _keyboardBrightness = brightness;
        if (updateSlider) _keyBrightnessSlider.Value = brightness;
        _keyBrightnessText.Text = $"{brightness}%";
    }

    private void SetLightingSpeed(int speed, bool updateSlider = true)
    {
        _lightingSpeed = speed;
        if (updateSlider) _lightingSpeedSlider.Value = speed;
        _lightSpeedTextBlock.Text = speed.ToString();
    }

    private static string ToRgbHex(Color color)
    {
        return $"{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static void SetColorPicker(ColorPicker? picker, string rgbHex)
    {
        if (picker == null) return;
        var normalized = NormalizeRgbHex(rgbHex);
        if (normalized == null) return;
        picker.Color = Color.Parse($"#{normalized}");
    }

    private static string? NormalizeRgbHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hex = value.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6) return null;
        foreach (var c in hex)
            if (!Uri.IsHexDigit(c))
                return null;
        return hex.ToUpperInvariant();
    }

    private static bool TryParsePerZoneMode(string? value, out string zone1, out string zone2, out string zone3,
        out string zone4, out int brightness)
    {
        zone1 = zone2 = zone3 = zone4 = "";
        brightness = 100;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Trim().Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 5) return false;

        zone1 = NormalizeRgbHex(parts[0]) ?? "";
        zone2 = NormalizeRgbHex(parts[1]) ?? "";
        zone3 = NormalizeRgbHex(parts[2]) ?? "";
        zone4 = NormalizeRgbHex(parts[3]) ?? "";
        if (zone1.Length != 6 || zone2.Length != 6 || zone3.Length != 6 || zone4.Length != 6) return false;
        if (!int.TryParse(parts[4], out brightness)) return false;
        return brightness is >= 0 and <= 100;
    }

    private static bool TryParseFourZoneMode(string? value, out int mode, out int speed, out int brightness,
        out int direction, out int red, out int green, out int blue)
    {
        mode = 0;
        speed = 5;
        brightness = 100;
        direction = DirectionRightToLeft;
        red = green = blue = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Trim().Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 7) return false;

        return int.TryParse(parts[0], out mode) && mode is >= 0 and <= 7 &&
               int.TryParse(parts[1], out speed) && speed is >= 0 and <= 9 &&
               int.TryParse(parts[2], out brightness) && brightness is >= 0 and <= 100 &&
               int.TryParse(parts[3], out direction) && direction is >= DirectionLeftToRight and <= DirectionRightToLeft &&
               int.TryParse(parts[4], out red) && red is >= 0 and <= 255 &&
               int.TryParse(parts[5], out green) && green is >= 0 and <= 255 &&
               int.TryParse(parts[6], out blue) && blue is >= 0 and <= 255;
    }

    private void ApplySavedZonePresetToUI()
    {
        if (!File.Exists(KeyboardZonePresetPath)) return;
        try
        {
            var value = File.ReadAllText(KeyboardZonePresetPath).Trim();
            if (!TryParsePerZoneMode(value, out var z1, out var z2, out var z3, out var z4, out var brightness))
                return;
            SetColorPicker(_zone1ColorPicker, z1);
            SetColorPicker(_zone2ColorPicker, z2);
            SetColorPicker(_zone3ColorPicker, z3);
            SetColorPicker(_zone4ColorPicker, z4);
            _keyboardBrightness = brightness;
            _keyBrightnessSlider.Value = brightness;
            _keyBrightnessText.Text = $"{brightness}%";
        }
        catch
        {
            // Ignore broken preset files.
        }
    }

    private void SaveZonePresetFromUI()
    {
        try
        {
            Directory.CreateDirectory(AppDataFolderPath);
            File.WriteAllText(KeyboardZonePresetPath,
                $"{ToRgbHex(_zone1ColorPicker.Color)},{ToRgbHex(_zone2ColorPicker.Color)}," +
                $"{ToRgbHex(_zone3ColorPicker.Color)},{ToRgbHex(_zone4ColorPicker.Color)},{_keyboardBrightness}");
        }
        catch
        {
            // Saving UI presets must never break keyboard control.
        }
    }

    private void ApplySavedLightingEffectPresetToUI()
    {
        if (!File.Exists(KeyboardLightingEffectPresetPath)) return;
        try
        {
            var value = File.ReadAllText(KeyboardLightingEffectPresetPath).Trim();
            if (!TryParseFourZoneMode(value, out var mode, out var speed, out var brightness, out var direction,
                    out var red, out var green, out var blue)) return;

            _lightingModeComboBox.SelectedIndex = mode;
            SetLightingSpeed(speed);
            SetKeyboardBrightness(brightness);
            SetDirectionRadioButtons(direction);
            SetColorPicker(_lightEffectColorPicker, $"{red:X2}{green:X2}{blue:X2}");
        }
        catch
        {
            // Ignore broken preset files.
        }
    }

    private void SaveLightingEffectPresetFromUI(int mode, int direction, Color color)
    {
        try
        {
            Directory.CreateDirectory(AppDataFolderPath);
            File.WriteAllText(KeyboardLightingEffectPresetPath,
                $"{mode},{_lightingSpeed},{_keyboardBrightness},{direction},{color.R},{color.G},{color.B}");
        }
        catch
        {
            // Saving UI presets must never break keyboard control.
        }
    }

    private int GetSelectedDirection()
    {
        if (_leftToRightRadioButton?.IsChecked == true) return DirectionLeftToRight;
        if (_rightToLeftRadioButton?.IsChecked == true) return DirectionRightToLeft;
        return DirectionLeftToRight;
    }

    private void SetDirectionRadioButtons(int direction)
    {
        _leftToRightRadioButton.IsChecked = direction == DirectionLeftToRight;
        _rightToLeftRadioButton.IsChecked = direction == DirectionRightToLeft;
    }

    // ------------------------------------------------------------- tray / exit

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        MetricsPoller.SetVisible(true, _curveActive);
        _ = MetricsPoller.RefreshNowAsync();
    }

    public void ToggleFromTray()
    {
        if (IsVisible)
        {
            Hide();
            MetricsPoller.SetVisible(false, _curveActive);
        }
        else
        {
            ShowFromTray();
        }
    }

    public async Task SetFanAutoFromTrayAsync()
    {
        _autoFanSpeedRadioButton.IsChecked = true;
        if (!_isConnected) return;
        await RunSafeAsync(() => _client.SetFanSpeedAsync(0, 0), "Cooling error");
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (!_forceQuit && !_quitting && ProfileStore.Preferences.CloseToTray && App.TrayAvailable)
        {
            e.Cancel = true;
            Hide();
            // Hidden with no fan curve: drop telemetry polling to the slow lane.
            MetricsPoller.SetVisible(false, _curveActive);
            ToastService.Show("Still running", "DAMX was minimized to the tray.");
            return;
        }

        // Fan curves run in the GUI. Reset the fans before actually exiting so they
        // cannot be stranded at the last curve value: cancel this close, reset
        // asynchronously, then close for real.
        if (!_quitting && _curveActive && _isConnected)
        {
            e.Cancel = true;
            _quitting = true;
            _ = ResetFansAndCloseAsync();
            return;
        }

        MetricsPoller.Updated -= OnMetricsUpdated;
        MetricsPoller.Stop();
        _powerDetection?.Dispose();
    }

    private async Task ResetFansAndCloseAsync()
    {
        try
        {
            await _client.SetFanSpeedAsync(0, 0);
        }
        catch
        {
            // Best effort — the daemon may already be shutting down.
        }

        await Dispatcher.UIThread.InvokeAsync(Close);
    }

    public void QuitApplication()
    {
        _forceQuit = true;
        Close();
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Runs a daemon action, converting any failure into a toast instead of a crash.</summary>
    private static async Task RunSafeAsync(Func<Task> action, string errorTitle)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ToastService.Error(errorTitle, ex.Message);
        }
    }

    private static bool IsEnabledSetting(string? value)
    {
        return (value ?? "0").Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetUsbChargingIndex(string? value)
    {
        return value switch
        {
            "10" => 1,
            "20" => 2,
            "30" => 3,
            _ => 0
        };
    }

    private static int UsbIndexToLevel(int index)
    {
        return index switch
        {
            1 => 10,
            2 => 20,
            3 => 30,
            _ => 0
        };
    }

    private static int ApplyFanSpeed(string? value, ref int backingField, Slider? slider, TextBlock? textBlock)
    {
        if (!int.TryParse(value ?? "0", out var speed)) return 0;
        backingField = speed;
        if (slider != null) slider.Value = speed;
        if (textBlock != null) textBlock.Text = speed == 0 ? "Auto" : $"{speed}%";
        return speed;
    }

    private static void SetCheckBox(CheckBox? checkBox, bool value)
    {
        if (checkBox != null) checkBox.IsChecked = value;
    }

    private static void SetEnabled(Control? control, bool value)
    {
        if (control != null) control.IsEnabled = value;
    }

    // ------------------------------------------------------------- callbacks

    public void DeveloperMode_OnClick(object? sender, RoutedEventArgs e)
    {
        EnableDevMode(true);
    }

    public void EnableDevMode(bool toEnable)
    {
        AppState.DevMode = toEnable;
        if (_devModeToggle.IsChecked != toEnable)
        {
            _suppressToggleHandlers = true;
            _devModeToggle.IsChecked = toEnable;
            _suppressToggleHandlers = false;
        }

        ApplySettingsToUI();
    }

    private void UpdatesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/PXDiv/Div-Acer-Manager-Max/releases");
    }

    private void IssuePageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/PXDiv/Div-Acer-Manager-Max/issues");
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ToastService.Error("Could not open link", ex.Message);
        }
    }

    private void InternalsMangerWindow_OnClick(object? sender, RoutedEventArgs e)
    {
        var window = new InternalsManager(this);
        window.ShowDialog(this);
    }

    public static class AppState
    {
        public static bool DevMode { get; set; }
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
