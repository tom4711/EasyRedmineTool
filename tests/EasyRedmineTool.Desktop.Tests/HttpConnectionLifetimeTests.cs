namespace EasyRedmineTool.Desktop.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Desktop.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

public sealed class HttpConnectionLifetimeTests
{
    [Fact]
    public void Captured_typed_client_rotates_connections_without_replacing_its_handler()
    {
        using var services = new ServiceCollection().AddEasyRedmineTool().BuildServiceProvider();
        var name = typeof(IEasyRedmineApiClient).Name;
        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);
        while (handler is DelegatingHandler wrapper) handler = wrapper.InnerHandler!;
        var sockets = Assert.IsType<SocketsHttpHandler>(handler);

        Assert.Equal(TimeSpan.FromMinutes(5), sockets.PooledConnectionLifetime);
        Assert.Equal(Timeout.InfiniteTimeSpan,
            services.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(name).HandlerLifetime);
        Assert.Equal(TimeSpan.FromSeconds(100), services.GetRequiredService<IHttpClientFactory>().CreateClient(name).Timeout);
    }
}
