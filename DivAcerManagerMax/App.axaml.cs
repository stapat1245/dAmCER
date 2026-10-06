using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace DivAcerManagerMax;

public class App : Application
{
    /// <summary>True when the tray icon was created; close-to-tray is ignored otherwise.</summary>
    public static bool TrayAvailable { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            SetupTray(mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetupTray(MainWindow mainWindow)
    {
        try
        {
            var tray = new TrayIcon
            {
                Icon = mainWindow.Icon,
                ToolTipText = "Div Acer Manager Max",
                IsVisible = true
            };

            var menu = new NativeMenu();

            var open = new NativeMenuItem("Open DAMX");
            open.Click += (_, _) => Safe(() => mainWindow.ShowFromTray());
            menu.Items.Add(open);

            var toggle = new NativeMenuItem("Show / hide window");
            toggle.Click += (_, _) => Safe(mainWindow.ToggleFromTray);
            menu.Items.Add(toggle);

            var profiles = new NativeMenuItem("Performance profile");
            var profilesMenu = new NativeMenu();
            AddProfileItem(profilesMenu, mainWindow, "Eco", "low-power");
            AddProfileItem(profilesMenu, mainWindow, "Quiet", "quiet");
            AddProfileItem(profilesMenu, mainWindow, "Balanced", "balanced");
            AddProfileItem(profilesMenu, mainWindow, "Performance", "balanced-performance");
            AddProfileItem(profilesMenu, mainWindow, "Turbo", "performance");
            profiles.Menu = profilesMenu;
            menu.Items.Add(profiles);

            var fanAuto = new NativeMenuItem("Fan: automatic");
            fanAuto.Click += async (_, _) => await SafeAsync(() => mainWindow.SetFanAutoFromTrayAsync());
            menu.Items.Add(fanAuto);

            menu.Items.Add(new NativeMenuItemSeparator());

            var quit = new NativeMenuItem("Quit");
            quit.Click += (_, _) => Safe(mainWindow.QuitApplication);
            menu.Items.Add(quit);

            tray.Menu = menu;
            tray.Clicked += (_, _) => Safe(mainWindow.ToggleFromTray);

            TrayIcon.SetIcons(Current!, new TrayIcons { tray });
            TrayAvailable = true;
        }
        catch (Exception ex)
        {
            TrayAvailable = false;
            Console.WriteLine($"Tray icon unavailable: {ex.Message}");
        }
    }

    private static void AddProfileItem(NativeMenu menu, MainWindow window, string label, string profile)
    {
        var item = new NativeMenuItem(label);
        item.Click += async (_, _) => await SafeAsync(() => window.ApplyQuickProfileAsync(profile));
        menu.Items.Add(item);
    }

    private static void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tray action failed: {ex.Message}");
        }
    }

    private static async Task SafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tray action failed: {ex.Message}");
        }
    }
}
