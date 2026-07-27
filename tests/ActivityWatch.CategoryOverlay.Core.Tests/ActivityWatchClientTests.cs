using System.Net;
using System.Text;
using System.Text.Json;
using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class ActivityWatchClientTests
{
    [Fact]
    public async Task GetSnapshot_parses_settings_categories_and_durations()
    {
        var handler = new RecordingActivityWatchHandler();
        var client = CreateClient(handler);

        var snapshot = await client.GetSnapshotAsync(
            DateTimeOffset.Parse("2026-07-27T04:00:00-07:00"),
            DateTimeOffset.Parse("2026-07-28T04:00:00-07:00"),
            CancellationToken.None);

        Assert.Equal(new TimeOnly(4, 0), snapshot.StartOfDay);
        Assert.Contains(
            snapshot.AvailableCategories,
            path => path.SequenceEqual(["Work", "Research"]));
        var research = Assert.Single(
            snapshot.Durations,
            duration => duration.Path.SequenceEqual(["Work", "Research"]));
        Assert.Equal(TimeSpan.FromMinutes(90), research.Duration);
    }

    [Fact]
    public async Task GetSnapshot_uses_only_get_and_query_post()
    {
        var handler = new RecordingActivityWatchHandler();
        var client = CreateClient(handler);

        await client.GetSnapshotAsync(
            DateTimeOffset.Parse("2026-07-27T04:00:00-07:00"),
            DateTimeOffset.Parse("2026-07-28T04:00:00-07:00"),
            CancellationToken.None);

        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method != HttpMethod.Get
                && request.RequestUri.AbsolutePath != "/api/0/query/");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method != HttpMethod.Get
                && (request.RequestUri.AbsolutePath.Contains("/events", StringComparison.Ordinal)
                    || request.RequestUri.AbsolutePath.Contains(
                        "/settings",
                        StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GetSnapshot_posts_server_side_categorization_query()
    {
        var handler = new RecordingActivityWatchHandler();
        var client = CreateClient(handler);

        await client.GetSnapshotAsync(
            DateTimeOffset.Parse("2026-07-27T04:00:00-07:00"),
            DateTimeOffset.Parse("2026-07-28T04:00:00-07:00"),
            CancellationToken.None);

        var queryRequest = Assert.Single(
            handler.Requests,
            request => request.RequestUri.AbsolutePath == "/api/0/query/");
        using var payload = JsonDocument.Parse(queryRequest.Body);
        var query = payload.RootElement
            .GetProperty("query")
            .EnumerateArray()
            .Select(statement => statement.GetString()!)
            .ToArray();
        var timePeriods = payload.RootElement
            .GetProperty("timeperiods")
            .EnumerateArray()
            .Select(period => period.GetString()!)
            .ToArray();

        Assert.Contains(query, statement => statement.StartsWith(
            "events = categorize(events",
            StringComparison.Ordinal));
        Assert.Contains(
            "cat_events = sort_by_duration(merge_events_by_keys(events, [\"$category\"]));",
            query);
        Assert.Contains(
            "2026-07-27T04:00:00.0000000-07:00/2026-07-28T04:00:00.0000000-07:00",
            timePeriods);
    }

    private static ActivityWatchClient CreateClient(HttpMessageHandler handler)
    {
        return new ActivityWatchClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5600"),
            });
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri RequestUri, string Body);

    private sealed class RecordingActivityWatchHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));

            var json = request.RequestUri!.AbsolutePath switch
            {
                "/api/0/settings" => """{"startOfDay":"04:00"}""",
                "/api/0/settings/classes" => """
                    [
                      {
                        "name":["Work","Research"],
                        "rule":{"type":"regex","regex":"paper","ignore_case":true}
                      }
                    ]
                    """,
                "/api/0/buckets/" => """
                    {
                      "aw-watcher-window_MSI":{
                        "id":"aw-watcher-window_MSI",
                        "type":"currentwindow",
                        "hostname":"MSI"
                      },
                      "aw-watcher-afk_MSI":{
                        "id":"aw-watcher-afk_MSI",
                        "type":"afkstatus",
                        "hostname":"MSI"
                      }
                    }
                    """,
                "/api/0/query/" => """
                    [
                      {
                        "cat_events":[
                          {
                            "duration":5400,
                            "data":{"$category":["Work","Research"]}
                          },
                          {
                            "duration":300,
                            "data":{"$category":["Uncategorized"]}
                          }
                        ]
                      }
                    ]
                    """,
                _ => throw new InvalidOperationException(
                    $"Unexpected path: {request.RequestUri}"),
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
