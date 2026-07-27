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

        var windowBucket = buckets.Values
            .Where(bucket => bucket.Type == "currentwindow")
            .OrderBy(bucket => bucket.Id, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new HttpRequestException("No currentwindow bucket is available.");
        var afkBucket = buckets.Values.FirstOrDefault(
            bucket => bucket.Type == "afkstatus"
                && bucket.Hostname == windowBucket.Hostname)
            ?? throw new HttpRequestException(
                $"No afkstatus bucket matches host {windowBucket.Hostname}.");

        var queryClasses = classes
            .Select(category => new object[] { category.Name, category.Rule })
            .ToArray();
        var classesJson = JsonSerializer.Serialize(queryClasses, JsonOptions);
        var query = ActivityWatchQueryBuilder.Build(
            windowBucket.Id,
            afkBucket.Id,
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

    private sealed record SettingsDto(string StartOfDay);

    private sealed record CategoryRuleDto(
        IReadOnlyList<string> Name,
        JsonElement Rule);

    private sealed record BucketDto(
        string Id,
        string Type,
        string Hostname);

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
