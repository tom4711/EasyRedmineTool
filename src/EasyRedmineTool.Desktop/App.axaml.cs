using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using EasyRedmineTool.Desktop.DependencyInjection;
using EasyRedmineTool.Desktop.ViewModels;
using EasyRedmineTool.Desktop.Views;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq;

namespace EasyRedmineTool.Desktop;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Code zum Entfernen von doppelten Validierungen wurde entfernt,
        // da BindingPlugins in dieser Avalonia-Version nicht zugänglich ist.
        // Dies kann in neueren Versionen wieder hinzugefügt werden.

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEasyRedmineTool();

        Services = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}