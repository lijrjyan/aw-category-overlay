using ActivityWatch.CategoryOverlay.Core.Abstractions;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class OverlayStateServiceTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-07-27T14:30:00-07:00");

    [Fact]
    public async Task Refresh_keeps_manual_order_and_includes_available_zero_duration()
    {
        var targets = new[]
        {
            Target(["Work", "LeetCode"], order: 0),
            Target(["Work", "Research"], order: 1),
        };
        var client = FakeActivityWatchClient.Returning(
            Snapshot(
                [["Work", "LeetCode"], ["Work", "Research"]],
                [new CategoryDuration(["Work", "Research"], TimeSpan.FromMinutes(90))]));
        var service = CreateService(client, targets);

        var state = await service.RefreshAsync(CancellationToken.None);

        Assert.Equal(
            ["Work › LeetCode", "Work › Research"],
            state.Rows.Select(row => row.Target.DisplayName));
        Assert.Equal(TimeSpan.Zero, state.Rows[0].Actual);
        Assert.Equal(TimeSpan.FromMinutes(90), state.Rows[1].Actual);
    }

    [Fact]
    public async Task Failed_refresh_preserves_last_rows_and_marks_state_stale()
    {
        var client = FakeActivityWatchClient.ReturnThenThrow(
            Snapshot(
                [["Work", "Research"]],
                [new CategoryDuration(["Work", "Research"], TimeSpan.FromMinutes(90))]),
            new HttpRequestException("offline"));
        var service = CreateService(client, [Target(["Work", "Research"], 0)]);

        var fresh = await service.RefreshAsync(CancellationToken.None);
        var stale = await service.RefreshAsync(CancellationToken.None);

        Assert.False(fresh.IsStale);
        Assert.True(stale.IsStale);
        Assert.Same(fresh.Rows, stale.Rows);
        Assert.Equal(fresh.LastSuccessfulRefresh, stale.LastSuccessfulRefresh);
    }

    [Fact]
    public async Task Refresh_omits_target_whose_category_no_longer_exists()
    {
        var targets = new[]
        {
            Target(["Work", "Research"], order: 0),
            Target(["Work", "Removed"], order: 1),
        };
        var client = FakeActivityWatchClient.Returning(
            Snapshot(
                [["Work", "Research"]],
                [new CategoryDuration(["Work", "Research"], TimeSpan.FromMinutes(90))]));
        var service = CreateService(client, targets);

        var state = await service.RefreshAsync(CancellationToken.None);

        var row = Assert.Single(state.Rows);
        Assert.Equal("Work › Research", row.Target.DisplayName);
    }

    [Fact]
    public async Task Refresh_requeries_when_server_day_boundary_differs()
    {
        var first = Snapshot(
            [["Work", "Research"]],
            [],
            startOfDay: new TimeOnly(3, 0));
        var second = Snapshot(
            [["Work", "Research"]],
            [new CategoryDuration(["Work", "Research"], TimeSpan.FromMinutes(30))],
            startOfDay: new TimeOnly(3, 0));
        var client = FakeActivityWatchClient.Returning(first, second);
        var service = CreateService(client, [Target(["Work", "Research"], 0)]);

        var state = await service.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, client.Windows.Count);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-27T03:00:00-07:00"),
            client.Windows[1].Start);
        Assert.Equal(TimeSpan.FromMinutes(30), state.Rows[0].Actual);
    }

    private static CategoryTarget Target(IReadOnlyList<string> path, int order)
    {
        return new CategoryTarget(
            path,
            ThresholdDirection.Minimum,
            TimeSpan.FromHours(1),
            order);
    }

    private static ActivityWatchSnapshot Snapshot(
        IReadOnlyList<IReadOnlyList<string>> available,
        IReadOnlyList<CategoryDuration> durations,
        TimeOnly? startOfDay = null)
    {
        return new ActivityWatchSnapshot(
            startOfDay ?? new TimeOnly(4, 0),
            available,
            durations);
    }

    private static OverlayStateService CreateService(
        IActivityWatchClient client,
        IReadOnlyList<CategoryTarget> targets)
    {
        return new OverlayStateService(
            client,
            new FakeClock(Now),
            OverlayConfiguration.Default with { Targets = targets });
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;

        public TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
    }

    private sealed class FakeActivityWatchClient : IActivityWatchClient
    {
        private readonly Queue<object> _results;

        private FakeActivityWatchClient(IEnumerable<object> results)
        {
            _results = new Queue<object>(results);
        }

        public List<LogicalDayWindow> Windows { get; } = [];

        public static FakeActivityWatchClient Returning(
            params ActivityWatchSnapshot[] snapshots)
        {
            return new FakeActivityWatchClient(snapshots);
        }

        public static FakeActivityWatchClient ReturnThenThrow(
            ActivityWatchSnapshot snapshot,
            Exception exception)
        {
            return new FakeActivityWatchClient([snapshot, exception]);
        }

        public Task<ActivityWatchSnapshot> GetSnapshotAsync(
            DateTimeOffset start,
            DateTimeOffset end,
            CancellationToken cancellationToken)
        {
            Windows.Add(new LogicalDayWindow(start, end));
            var result = _results.Dequeue();
            return result switch
            {
                ActivityWatchSnapshot snapshot => Task.FromResult(snapshot),
                Exception exception => Task.FromException<ActivityWatchSnapshot>(exception),
                _ => throw new InvalidOperationException(),
            };
        }
    }
}
