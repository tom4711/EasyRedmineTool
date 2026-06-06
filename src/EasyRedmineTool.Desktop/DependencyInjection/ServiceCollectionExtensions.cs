namespace EasyRedmineTool.Desktop.DependencyInjection;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;
using EasyRedmineTool.Desktop.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEasyRedmineTool(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options => options.SingleLine = true);
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient<EasyRedmineApiClient>();

        services.AddSingleton<IConnectionTestService, ConnectionTestService>();
        services.AddSingleton<ITicketService, TicketService>();
        services.AddSingleton<ITimeEntryService, TimeEntryService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<TicketListViewModel>();
        services.AddTransient<TimeEntriesViewModel>();
        services.AddTransient<WeeklySummaryViewModel>();


        return services;
    }
}

