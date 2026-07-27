namespace ActivityWatch.CategoryOverlay.Core.Models;

public sealed record CategoryDuration(
    IReadOnlyList<string> Path,
    TimeSpan Duration);

public sealed record ActivityWatchSnapshot(
    TimeOnly StartOfDay,
    IReadOnlyList<IReadOnlyList<string>> AvailableCategories,
    IReadOnlyList<CategoryDuration> Durations);

