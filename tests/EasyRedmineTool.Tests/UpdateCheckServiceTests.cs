namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using System.Net;
using System.Text;

public class AppVersionTests
{
    [Theory]
    [InlineData("v0.9.0", "0.8.0", true)]
    [InlineData("0.9.0", "0.8.0", true)]
    [InlineData("v0.8.0", "0.8.0", false)]
    [InlineData("v0.8.0", "0.8.0+Branch.main.Sha.abc", false)]
    [InlineData("v0.8.1", "0.8.0", true)]
    [InlineData("v0.8.0", "0.9.0", false)]
    public void IsNewer_compares_semver_tags(string latest, string current, bool expected) =>
        Assert.Equal(expected, AppVersion.IsNewer(latest, current));

    [Theory]
    [InlineData("v0.8.0", 0, 8, 0)]
    [InlineData("0.8.0-beta.1", 0, 8, 0)]
    public void TryParse_normalizes_tags(string input, int major, int minor, int build)
    {
        Assert.True(AppVersion.TryParse(input, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(build, version.Build);
    }
}

public class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_returns_update_when_latest_release_is_newer()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"tag_name":"v99.0.0","html_url":"https://github.com/tom4711/EasyRedmineTool/releases/tag/v99.0.0"}""",
                Encoding.UTF8,
                "application/json")
        });
        var service = CreateService(handler);

        var result = await service.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("99.0.0", result.LatestVersion);
        Assert.Equal("https://github.com/tom4711/EasyRedmineTool/releases/tag/v99.0.0", result.ReleaseUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_returns_up_to_date_when_release_matches_current_version()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"tag_name":"v{{AppVersion.Current}}","html_url":"https://github.com/tom4711/EasyRedmineTool/releases/latest"}""",
                Encoding.UTF8,
                "application/json")
        });
        var service = CreateService(handler);

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_returns_up_to_date_on_http_error()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateService(handler);

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    private static UpdateCheckService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<UpdateCheckService>.Instance);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
