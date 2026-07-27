using System.Text.Json;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public static class ActivityWatchQueryBuilder
{
    public static string[] Build(
        string windowBucket,
        string afkBucket,
        string classesJson)
    {
        return
        [
            $"events = flood(query_bucket({JsonSerializer.Serialize(windowBucket)}));",
            $"not_afk = flood(query_bucket({JsonSerializer.Serialize(afkBucket)}));",
            "not_afk = filter_keyvals(not_afk, \"status\", [\"not-afk\"]);",
            "events = filter_period_intersect(events, not_afk);",
            $"events = categorize(events, {classesJson});",
            "cat_events = sort_by_duration(merge_events_by_keys(events, [\"$category\"]));",
            "RETURN = {\"cat_events\": cat_events};",
        ];
    }
}

