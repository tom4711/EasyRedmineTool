namespace EasyRedmineTool.Desktop.DependencyInjection;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;
using EasyRedmineTool.Desktop.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Net.Http;
using System.Threading;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEasyRedmineTool(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options => options.SingleLine = true);
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Singleton services retain this typed client for the desktop session.
        // Rotate pooled connections for DNS/VPN changes; keep the owning handler stable.
        services.AddHttpClient<IEasyRedmineApiClient, EasyRedmineApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddHttpClient<IUpdateCheckService, UpdateCheckService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IConnectionTestService, ConnectionTestService>();
        services.AddSingleton<ITicketService, TicketService>();
        services.AddSingleton<ITimeEntryService, TimeEntryService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<TicketListViewModel>();
        services.AddTransient<KanbanDashboardViewModel>();
        services.AddTransient<TimeEntriesViewModel>();
        services.AddTransient<WeeklySummaryViewModel>();
        services.AddTransient<SeriesBookingViewModel>();

        return services;
    }
}

