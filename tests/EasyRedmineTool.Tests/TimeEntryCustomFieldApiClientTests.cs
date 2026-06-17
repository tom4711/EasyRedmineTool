namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;

using System.Net;
using System.Text;

public class TimeEntryCustomFieldApiClientTests
{
    [Fact]
    public async Task GetTimeEntryCustomFieldDefinitionsAsync_parses_global_custom_fields()
    {
        const string json = """
            {
              "custom_fields": [
                {
                  "id": 4,
                  "name": "Kostenstelle",
                  "customized_type": "time_entry",
                  "field_format": "list",
                  "is_required": true,
                  "is_for_all": false,
                  "projects": [{ "id": 42, "name": "Projekt A" }],
                  "possible_values": [{ "value": "Intern" }, { "value": "Extern" }]
                },
                {
                  "id": 9,
                  "name": "Issue Feld",
                  "customized_type": "issue",
                  "field_format": "string"
                }
              ]
            }
            """;

        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var definitions = await client.GetTimeEntryCustomFieldDefinitionsAsync(
            "https://redmine.example/",
            "secret",
            projectId: 42);

        Assert.Single(definitions);
        Assert.Equal(4, definitions[0].Id);
        Assert.Equal("Kostenstelle", definitions[0].Name);
        Assert.True(definitions[0].IsRequired);
        Assert.False(definitions[0].IsForAll);
        Assert.Equal([42], definitions[0].ProjectIds);
        Assert.Equal(["Intern", "Extern"], definitions[0].PossibleValues);
        Assert.True(definitions[0].HasPossibleValues);
    }

    [Fact]
    public async Task GetTimeEntryCustomFieldDefinitionsAsync_prefers_project_payload()
    {
        const string projectJson = """
            {
              "project": {
                "id": 42,
                "time_entry_custom_fields": [
                  {
                    "id": 7,
                    "name": "Abrechenbar",
                    "field_format": "list",
                    "possible_values": ["Ja", "Nein"]
                  }
                ]
              }
            }
            """;

        var client = CreateClient(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/projects/42.", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(projectJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        });

        var definitions = await client.GetTimeEntryCustomFieldDefinitionsAsync(
            "https://redmine.example/",
            "secret",
            projectId: 42);

        Assert.Single(definitions);
        Assert.Equal(7, definitions[0].Id);
        Assert.Equal("Abrechenbar", definitions[0].Name);
        Assert.True(definitions[0].HasPossibleValues);
    }

    [Fact]
    public async Task GetTimeEntryCustomFieldDefinitionsAsync_ignores_issue_custom_fields_on_project_payload()
    {
        const string projectJson = """
            {
              "project": {
                "id": 42,
                "custom_fields": [
                  {
                    "id": 1,
                    "name": "Issue Feld",
                    "customized_type": "issue",
                    "field_format": "string"
                  },
                  {
                    "id": 2,
                    "name": "Projektzuordnung",
                    "customized_type": "issue",
                    "field_format": "list"
                  }
                ]
              }
            }
            """;

        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(projectJson, Encoding.UTF8, "application/json")
        });

        var definitions = await client.GetTimeEntryCustomFieldDefinitionsAsync(
            "https://redmine.example/",
            "secret",
            projectId: 42);

        Assert.Empty(definitions);
    }

    [Fact]
    public async Task GetTimeEntryCustomFieldDefinitionsAsync_requests_activity_specific_issue_endpoint()
    {
        string? requestedPath = null;
        const string json = """
            {
              "time_entry_custom_fields": [
                {
                  "id": 5,
                  "name": "Produktdaten Hierarchie",
                  "field_format": "depending_enumeration",
                  "activity_ids": [100]
                }
              ]
            }
            """;

        var client = CreateClient(request =>
        {
            requestedPath = request.RequestUri!.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var definitions = await client.GetTimeEntryCustomFieldDefinitionsAsync(
            "https://redmine.example/",
            "secret",
            issueId: 22658,
            projectId: 42,
            activityId: 100);

        Assert.Equal("/issues/22658/time_entry_custom_fields.json?activity_id=100", requestedPath);
        Assert.Single(definitions);
        Assert.Equal(5, definitions[0].Id);
        Assert.Equal([100], definitions[0].ActivityIds);
    }

    private static EasyRedmineApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        new(new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://redmine.example/")
        });

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
