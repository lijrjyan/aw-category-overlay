using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class ActivityWatchLiveParityTests
{
    [Fact]
    public async Task Live_query_is_read_only_and_returns_category_totals()
    {
        if (Environment.GetEnvironmentVariable("AW_OVERLAY_RUN_LIVE_TESTS") != "1")
        {
            return;
        }

        var recorder = new RecordingHandler(
            new HttpClientHandler
            {
                UseProxy = false,
            });
        using var httpClient = new HttpClient(recorder)
        {
            BaseAddress = new Uri("http://localhost:5600"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        var client = new ActivityWatchClient(httpClient);
        var now = DateTimeOffset.Now;
        var window = LogicalDayCalculator.GetWindow(
            now,
            new TimeOnly(4, 0),
            TimeZoneInfo.Local);

        var snapshot = await client.GetSnapshotAsync(
            window.Start,
            window.End,
            CancellationToken.None);
        if (snapshot.StartOfDay != new TimeOnly(4, 0))
        {
            window = LogicalDayCalculator.GetWindow(
                now,
                snapshot.StartOfDay,
                TimeZoneInfo.Local);
            snapshot = await client.GetSnapshotAsync(
                window.Start,
                window.End,
                CancellationToken.None);
        }

        Assert.NotEmpty(snapshot.AvailableCategories);
        Assert.All(snapshot.Durations, duration =>
        {
            Assert.NotEmpty(duration.Path);
            Assert.True(duration.Duration >= TimeSpan.Zero);
        });
        Assert.Contains(
            recorder.Requests,
            request => request.Method == HttpMethod.Post
                && request.Path == "/api/0/query/");
        Assert.DoesNotContain(
            recorder.Requests,
            request => request.Method != HttpMethod.Get
                && request.Path != "/api/0/query/");
        Assert.DoesNotContain(
            recorder.Requests,
            request => request.Method != HttpMethod.Get
                && (request.Path.Contains("/events", StringComparison.Ordinal)
                    || request.Path.Contains("/settings", StringComparison.Ordinal)));
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path);

    private sealed class RecordingHandler(HttpMessageHandler innerHandler)
        : DelegatingHandler(innerHandler)
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsolutePath));
            return base.SendAsync(request, cancellationToken);
        }
    }
}
