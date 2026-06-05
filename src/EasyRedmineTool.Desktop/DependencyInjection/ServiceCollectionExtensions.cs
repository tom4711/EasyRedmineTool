using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;
using EasyRedmineTool.Desktop.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace EasyRedmineTool.Desktop.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEasyRedmineTool(this IServiceCollection services)
    {
        services.AddHttpClient<EasyRedmineApiClient>();

        services.AddSingleton<IAuthService, AuthService>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}

