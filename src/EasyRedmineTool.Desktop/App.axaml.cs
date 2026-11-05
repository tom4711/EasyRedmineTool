namespace EasyRedmineTool.Desktop;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Avalonia.Platform;

using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Desktop.DependencyInjection;
using EasyRedmineTool.Desktop.Platform;
using EasyRedmineTool.Desktop.ViewModels;
using EasyRedmineTool.Desktop.Views;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }
    private bool _shutdownRequested;
    private bool _shutdownComplete;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEasyRedmineTool();
        Services = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = Services.GetRequiredService<IAppSettingsService>();
            var window = new MainWindow();
            var content = window.Content;
            window.Content = new TextBlock { Text = "Einstellungen werden geladen …", Margin = new Thickness(24) };
            desktop.MainWindow = window;
            _ = InitializeDesktopAsync(window, content, settings);
            desktop.ShutdownRequested += (_, e) =>
            {
                if (_shutdownComplete) return;
                e.Cancel = true;
                if (_shutdownRequested) return;
                _shutdownRequested = true;
                _ = FlushAndShutdownAsync(desktop, settings);
            };
            desktop.Exit += (_, _) => (Services as IDisposable)?.Dispose();
        }

        ApplyMacOsDockIcon();

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeDesktopAsync(MainWindow window, object? content, IAppSettingsService settings)
    {
        try
        {
            await settings.InitializeAsync();
            if (_shutdownRequested) return;
            window.DataContext = Services!.GetRequiredService<MainWindowViewModel>();
            window.Content = content;
        }
        catch (Exception)
        {
            window.Content = new TextBlock
            {
                Text = "Die Anwendung konnte nicht initialisiert werden. Bitte prüfen Sie den Zugriff auf die lokalen Einstellungen.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(24)
            };
        }
    }

    private async Task FlushAndShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop, IAppSettingsService settings)
    {
        while (true)
        {
            try
            {
                var flushTask = settings.FlushAsync();
                using var timeout = new CancellationTokenSource();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), timeout.Token);
                var completedTask = await Task.WhenAny(flushTask, timeoutTask);
                if (completedTask == flushTask)
                {
                    timeout.Cancel();
                    await flushTask;
                    break;
                }

                _ = flushTask.ContinueWith(
                    task => _ = task.Exception,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception)
            {
                // Offer a retry or an explicit choice to exit without the pending settings.
            }

            Services?.GetService<ILogger<App>>()?.LogWarning("Pending settings could not be persisted at shutdown.");
            if (!await ShowPersistenceFailureDialogAsync(desktop.MainWindow)) break;
        }

        _shutdownComplete = true;
        desktop.Shutdown();
    }

    private static async Task<bool> ShowPersistenceFailureDialogAsync(Window? owner)
    {
        if (owner is null) return false;

        var dialog = new Window
        {
            Title = "Einstellungen nicht gespeichert",
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        var retry = new Button { Content = "Erneut versuchen" };
        retry.Click += (_, _) => dialog.Close(true);
        var exit = new Button { Content = "Trotzdem beenden" };
        exit.Click += (_, _) => dialog.Close(false);
        buttons.Children.Add(retry);
        buttons.Children.Add(exit);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "Die letzten Einstellungen konnten nicht gespeichert werden. Sie können es erneut versuchen oder die Anwendung jetzt beenden.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                buttons
            }
        };
        return await dialog.ShowDialog<bool>(owner);
    }

    private static void ApplyMacOsDockIcon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            using var icnsStream = AssetLoader.Open(new Uri("avares://EasyRedmineTool.Desktop/Assets/app-icon.icns"));
            MacOsDockIcon.ApplyFromStream(icnsStream, ".icns");
        }
        catch
        {
            try
            {
                using var pngStream = AssetLoader.Open(new Uri("avares://EasyRedmineTool.Desktop/Assets/app-icon.png"));
                MacOsDockIcon.ApplyFromStream(pngStream, ".png");
            }
            catch
            {
                // Dock icon is optional; .app bundles use Info.plist instead.
            }
        }
    }
}
