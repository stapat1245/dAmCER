using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;

namespace DivAcerManagerMax.Services;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
///     Lightweight in-app notification stack. Host is wired up by MainWindow.
/// </summary>
public static class ToastService
{
    public static StackPanel? Host { get; set; }

    public static bool Enabled { get; set; } = true;

    public static void Show(string title, string message, ToastKind kind = ToastKind.Info)
    {
        if (!Enabled || Host == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var toast = Build(title, message, kind);
                Host.Children.Insert(0, toast);
                while (Host.Children.Count > 4) Host.Children.RemoveAt(Host.Children.Count - 1);
                Dispatcher.UIThread.Post(() => toast.Opacity = 1, DispatcherPriority.Background);
                _ = DismissAsync(toast);
            }
            catch
            {
                // Notifications are best-effort.
            }
        });
    }

    public static void Success(string title, string message)
    {
        Show(title, message, ToastKind.Success);
    }

    public static void Error(string title, string message)
    {
        Show(title, message, ToastKind.Error);
    }

    public static void Warning(string title, string message)
    {
        Show(title, message, ToastKind.Warning);
    }

    private static IBrush Resource(string key, IBrush fallback)
    {
        return Application.Current?.FindResource(key) as IBrush ?? fallback;
    }

    private static Border Build(string title, string message, ToastKind kind)
    {
        var (accentKey, icon) = kind switch
        {
            ToastKind.Success => ("DmxSuccess", MaterialIconKind.CheckCircleOutline),
            ToastKind.Warning => ("DmxWarning", MaterialIconKind.AlertOutline),
            ToastKind.Error => ("DmxDanger", MaterialIconKind.AlertCircleOutline),
            _ => ("DmxAccent", MaterialIconKind.InformationOutline)
        };

        var accent = Resource(accentKey, Brushes.SteelBlue);

        var glyph = new MaterialIcon
        {
            Kind = icon,
            Width = 18,
            Height = 18,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0)
        };

        var text = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 12.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Resource("DmxText", Brushes.White)
                },
                new TextBlock
                {
                    Text = message,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 320,
                    Foreground = Resource("DmxTextSecondary", Brushes.Gray)
                }
            }
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(12, 10),
            Children = { glyph, text }
        };
        Grid.SetColumn(text, 1);

        var strip = new Border
        {
            Background = accent,
            CornerRadius = new CornerRadius(10, 0, 0, 10),
            Margin = new Thickness(1, 5, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3,*"),
            Children = { strip, body }
        };
        Grid.SetColumn(body, 1);

        return new Border
        {
            Background = Resource("DmxSurfaceAlt", Brushes.Black),
            BorderBrush = Resource("DmxBorderStrong", Brushes.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = 0,
            Child = grid,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 18,
                OffsetY = 6,
                Color = Color.FromArgb(90, 0, 0, 0)
            }),
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(180),
                    Easing = new CubicEaseOut()
                }
            }
        };
    }

    private static async Task DismissAsync(Border toast)
    {
        await Task.Delay(4200);

        Dispatcher.UIThread.Post(() => toast.Opacity = 0);
        await Task.Delay(220);
        Dispatcher.UIThread.Post(() => Host?.Children.Remove(toast));
    }
}
