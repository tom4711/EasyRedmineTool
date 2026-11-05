namespace EasyRedmineTool.Desktop.Tests;

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Platform;
using EasyRedmineTool.Desktop.Views;
using Material.Icons.Avalonia;

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        AssetLoader.SetDefaultAssembly(typeof(TimeEntriesView).Assembly);
        Styles.Add(new FluentTheme());
        Styles.Add(new MaterialIconStyles(null));
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://EasyRedmineTool.Desktop/"))
        {
            Source = new Uri("avares://EasyRedmineTool.Desktop/Styles/ThemeBrushes.axaml")
        });
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://EasyRedmineTool.Desktop/"))
        {
            Source = new Uri("avares://EasyRedmineTool.Desktop/Styles/Icons.axaml")
        });
        Styles.Add(new StyleInclude(new Uri("avares://EasyRedmineTool.Desktop/"))
        {
            Source = new Uri("avares://EasyRedmineTool.Desktop/Styles/AppButtons.axaml")
        });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
