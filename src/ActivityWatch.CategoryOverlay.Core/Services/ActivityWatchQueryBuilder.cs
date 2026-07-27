using System.Text.Json;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public static class ActivityWatchQueryBuilder
{
    private static readonly BrowserDefinition[] Browsers =
    [
        new(
            "chrome",
            ["com.google.Chrome", "com.google.ChromeDev", "org.chromium.Chromium"],
            "(?i)^(google[-_ ]?chrome|chrome|chromium)"),
        new(
            "firefox",
            ["org.mozilla.firefox", "io.gitlab.librewolf-community", "net.waterfox.waterfox"],
            "(?i)(firefox|librewolf|waterfox|nightly)"),
        new("opera", ["com.opera.Opera"], "(?i)(opera)"),
        new("brave", ["com.brave.Browser"], "(?i)(brave)"),
        new(
            "edge",
            ["com.microsoft.Edge", "com.microsoft.EdgeDev"],
            "(?i)^(microsoft[-_ ]?edge|msedge)"),
        new("arc", [], "(?i)^arc(\\.exe)?$"),
        new("vivaldi", ["com.vivaldi.Vivaldi"], "(?i)(vivaldi)"),
        new("orion", ["Orion"], "(?i)(orion)"),
        new("yandex", ["ru.yandex.Browser"], "(?i)(yandex)"),
        new("zen", ["app.zen_browser.zen"], "(?i)(zen)"),
        new("floorp", ["one.ablaze.floorp"], "(?i)(floorp)"),
        new("helium", ["net.imput.helium"], "(?i)(helium)"),
    ];

    public static string[] Build(
        string windowBucket,
        string afkBucket,
        IReadOnlyList<string> browserBuckets,
        string? alwaysActivePattern,
        string classesJson)
    {
        List<string> query =
        [
            $"events = flood(query_bucket({JsonSerializer.Serialize(windowBucket)}));",
            $"not_afk = flood(query_bucket({JsonSerializer.Serialize(afkBucket)}));",
            "not_afk = filter_keyvals(not_afk, \"status\", [\"not-afk\"]);",
        ];

        if (!string.IsNullOrEmpty(alwaysActivePattern))
        {
            var serializedPattern = JsonSerializer.Serialize(alwaysActivePattern);
            query.Add(
                $"not_treat_as_afk = filter_keyvals_regex(events, \"app\", {serializedPattern});");
            query.Add("not_afk = period_union(not_afk, not_treat_as_afk);");
            query.Add(
                $"not_treat_as_afk = filter_keyvals_regex(events, \"title\", {serializedPattern});");
            query.Add("not_afk = period_union(not_afk, not_treat_as_afk);");
        }

        query.Add("browser_events = [];");
        foreach (var browser in Browsers)
        {
            var bucket = browserBuckets.FirstOrDefault(
                id => id.Contains(browser.Name, StringComparison.Ordinal));
            if (bucket is null)
            {
                continue;
            }

            var name = browser.Name;
            query.Add(
                $"events_{name} = flood(query_bucket({JsonSerializer.Serialize(bucket)}));");
            query.Add(
                $"window_{name} = filter_keyvals(events, \"app\", {JsonSerializer.Serialize(browser.AppNames)});");
            query.Add(
                $"window_{name}_re = filter_keyvals_regex(events, \"app\", {JsonSerializer.Serialize(browser.AppNameRegex)});");
            query.Add(
                $"window_{name} = sort_by_timestamp(concat(window_{name}, window_{name}_re));");
            query.Add(
                $"events_{name} = filter_period_intersect(events_{name}, window_{name});");
            query.Add($"events_{name} = split_url_events(events_{name});");
            query.Add($"browser_events = concat(browser_events, events_{name});");
            query.Add("browser_events = sort_by_timestamp(browser_events);");
        }

        query.Add(
            "audible_events = filter_keyvals(browser_events, \"audible\", [true]);");
        query.Add("not_afk = period_union(not_afk, audible_events);");
        query.Add("events = filter_period_intersect(events, not_afk);");
        query.Add($"events = categorize(events, {classesJson});");
        query.Add(
            "cat_events = sort_by_duration(merge_events_by_keys(events, [\"$category\"]));");
        query.Add("RETURN = {\"cat_events\": cat_events};");
        return [.. query];
    }

    private sealed record BrowserDefinition(
        string Name,
        string[] AppNames,
        string AppNameRegex);
}
