using ActivityWatch.CategoryOverlay.Core.Abstractions;
using ActivityWatch.CategoryOverlay.Core.Models;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public sealed record OverlayState(
    IReadOnlyList<OverlayRowState> Rows,
    TimeSpan TotalActiveTime,
    DateTimeOffset? LastSuccessfulRefresh,
    bool IsStale,
    IReadOnlyList<IReadOnlyList<string>> AvailableCategories)
{
    public static OverlayState Empty { get; } = new(
        [],
        TimeSpan.Zero,
        null,
        true,
        []);
}

public sealed class OverlayStateService(
    IActivityWatchClient activityWatchClient,
    IClock clock,
    OverlayConfiguration configuration)
{
    private static readonly CategoryPathComparer PathComparer = new();
    private OverlayConfiguration _configuration = configuration;
    private TimeOnly _startOfDay = new(4, 0);
    private OverlayState _state = OverlayState.Empty;

    public OverlayConfiguration Configuration => _configuration;

    public void UpdateConfiguration(OverlayConfiguration updated)
    {
        _configuration = updated;
    }

    public async Task<OverlayState> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var window = LogicalDayCalculator.GetWindow(
                clock.Now,
                _startOfDay,
                clock.LocalTimeZone);
            var snapshot = await activityWatchClient.GetSnapshotAsync(
                window.Start,
                window.End,
                cancellationToken);

            if (snapshot.StartOfDay != _startOfDay)
            {
                _startOfDay = snapshot.StartOfDay;
                window = LogicalDayCalculator.GetWindow(
                    clock.Now,
                    _startOfDay,
                    clock.LocalTimeZone);
                snapshot = await activityWatchClient.GetSnapshotAsync(
                    window.Start,
                    window.End,
                    cancellationToken);
            }

            var available = snapshot.AvailableCategories.ToHashSet(PathComparer);
            var totalActiveTime = snapshot.Durations.Aggregate(
                TimeSpan.Zero,
                (total, duration) => total + duration.Duration);
            var rows = _configuration.Targets
                .Where(target => available.Contains(target.Path))
                .OrderBy(target => target.Order)
                .Select(target => ThresholdEvaluator.Evaluate(
                    target,
                    SumDuration(target.Path, snapshot.Durations)))
                .ToList();

            _state = new OverlayState(
                rows,
                totalActiveTime,
                clock.Now,
                false,
                snapshot.AvailableCategories);
            return _state;
        }
        catch (HttpRequestException)
        {
            _state = _state with { IsStale = true };
            return _state;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _state = _state with { IsStale = true };
            return _state;
        }
    }

    private static TimeSpan SumDuration(
        IReadOnlyList<string> targetPath,
        IReadOnlyList<CategoryDuration> durations)
    {
        return durations
            .Where(duration => IsPathPrefix(targetPath, duration.Path))
            .Aggregate(
                TimeSpan.Zero,
                (total, duration) => total + duration.Duration);
    }

    private static bool IsPathPrefix(
        IReadOnlyList<string> prefix,
        IReadOnlyList<string> path)
    {
        return prefix.Count <= path.Count
            && prefix.SequenceEqual(
                path.Take(prefix.Count),
                StringComparer.Ordinal);
    }

    private sealed class CategoryPathComparer :
        IEqualityComparer<IReadOnlyList<string>>
    {
        public bool Equals(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left is not null
                && right is not null
                && left.SequenceEqual(right, StringComparer.Ordinal);
        }

        public int GetHashCode(IReadOnlyList<string> path)
        {
            var hash = new HashCode();
            foreach (var segment in path)
            {
                hash.Add(segment, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
