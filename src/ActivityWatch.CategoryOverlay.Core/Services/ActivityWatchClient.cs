using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityWatch.CategoryOverlay.Core.Abstractions;
using ActivityWatch.CategoryOverlay.Core.Models;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public sealed class ActivityWatchClient(HttpClient httpClient) : IActivityWatchClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<ActivityWatchSnapshot> GetSnapshotAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var settings = await GetRequiredAsync<SettingsDto>(
            "/api/0/settings",
            cancellationToken);
        var classes = await GetRequiredAsync<List<CategoryRuleDto>>(
            "/api/0/settings/classes",
            cancellationToken);
        var buckets = await GetRequiredAsync<Dictionary<string, BucketDto>>(
            "/api/0/buckets/",
            cancellationToken);

        var windowBuckets = buckets.Values
            .Where(bucket => bucket.Type == "currentwindow"
                && !bucket.Id.StartsWith(
                    "aw-watcher-android",
                    StringComparison.Ordinal))
            .ToList();
        var preferredHost = GetLandingPageHost(settings.Landingpage)
            ?? windowBuckets
                .OrderByDescending(bucket => bucket.LastUpdated)
                .Select(bucket => bucket.Hostname)
                .FirstOrDefault();
        var windowBucket = windowBuckets
            .FirstOrDefault(bucket => bucket.Hostname == preferredHost)
            ?? windowBuckets.FirstOrDefault()
            ?? throw new HttpRequestException("No currentwindow bucket is available.");
        var afkBucket = buckets.Values.FirstOrDefault(
            bucket => bucket.Type == "afkstatus"
                && bucket.Hostname == windowBucket.Hostname)
            ?? throw new HttpRequestException(
                $"No afkstatus bucket matches host {windowBucket.Hostname}.");
        var browserBuckets = buckets.Values
            .Where(bucket => bucket.Type == "web.tab.current"
                && bucket.Hostname == windowBucket.Hostname)
            .Select(bucket => bucket.Id)
            .ToList();
        if (browserBuckets.Count == 0)
        {
            browserBuckets = buckets.Values
                .Where(bucket => bucket.Type == "web.tab.current"
                    && bucket.Hostname == "unknown")
                .Select(bucket => bucket.Id)
                .ToList();
        }

        var queryClasses = classes
            .Select(category => new object[] { category.Name, category.Rule })
            .ToArray();
        var classesJson = JsonSerializer.Serialize(queryClasses, JsonOptions);
        var query = ActivityWatchQueryBuilder.Build(
            windowBucket.Id,
            afkBucket.Id,
            browserBuckets,
            settings.AlwaysActivePattern,
            classesJson);
        var timePeriod = string.Create(
            CultureInfo.InvariantCulture,
            $"{start:O}/{end:O}");

        using var response = await httpClient.PostAsJsonAsync(
            "/api/0/query/",
            new QueryRequest([timePeriod], query),
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var queryResults = await response.Content.ReadFromJsonAsync<List<QueryResultDto>>(
            JsonOptions,
            cancellationToken)
            ?? throw new HttpRequestException("ActivityWatch query returned no JSON.");
        var events = queryResults.FirstOrDefault()?.CategoryEvents
            ?? throw new HttpRequestException("ActivityWatch query returned no category events.");

        var available = classes
            .Select(category => (IReadOnlyList<string>)category.Name)
            .ToList();
        if (!available.Any(path => path.SequenceEqual(["Uncategorized"])))
        {
            available.Add(["Uncategorized"]);
        }

        var durations = events
            .Where(categoryEvent => categoryEvent.Data.Category.Count > 0)
            .Select(categoryEvent => new CategoryDuration(
                categoryEvent.Data.Category,
                TimeSpan.FromSeconds(categoryEvent.Duration)))
            .ToList();

        if (!TimeOnly.TryParseExact(
                settings.StartOfDay,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startOfDay))
        {
            throw new HttpRequestException(
                $"Invalid ActivityWatch startOfDay value: {settings.StartOfDay}");
        }

        return new ActivityWatchSnapshot(startOfDay, available, durations);
    }

    private static string? GetLandingPageHost(string? landingpage)
    {
        const string prefix = "/activity/";
        if (string.IsNullOrWhiteSpace(landingpage)
            || !landingpage.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var hostEnd = landingpage.IndexOf('/', prefix.Length);
        var escapedHost = hostEnd < 0
            ? landingpage[prefix.Length..]
            : landingpage[prefix.Length..hostEnd];
        return escapedHost.Length == 0
            ? null
            : Uri.UnescapeDataString(escapedHost);
    }

    private async Task<T> GetRequiredAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        return await httpClient.GetFromJsonAsync<T>(
            path,
            JsonOptions,
            cancellationToken)
            ?? throw new HttpRequestException($"ActivityWatch returned no JSON for {path}.");
    }

    private sealed record SettingsDto(
        string StartOfDay,
        string? Landingpage,
        [property: JsonPropertyName("always_active_pattern")]
        string? AlwaysActivePattern);

    private sealed record CategoryRuleDto(
        IReadOnlyList<string> Name,
        JsonElement Rule);

    private sealed record BucketDto(
        string Id,
        string Type,
        string Hostname,
        DateTimeOffset? LastUpdated);

    private sealed record QueryRequest(
        IReadOnlyList<string> Timeperiods,
        IReadOnlyList<string> Query);

    private sealed record QueryResultDto(
        [property: JsonPropertyName("cat_events")]
        IReadOnlyList<CategoryEventDto> CategoryEvents);

    private sealed record CategoryEventDto(
        double Duration,
        CategoryDataDto Data);

    private sealed record CategoryDataDto(
        [property: JsonPropertyName("$category")]
        IReadOnlyList<string> Category);
}
