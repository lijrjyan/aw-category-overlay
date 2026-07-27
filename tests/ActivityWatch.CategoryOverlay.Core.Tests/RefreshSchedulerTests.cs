using ActivityWatch.CategoryOverlay.Core.Abstractions;
using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class RefreshSchedulerTests
{
    [Theory]
    [InlineData("2026-07-27T10:02:17-07:00", 5, "2026-07-27T10:05:00-07:00")]
    [InlineData("2026-07-27T10:05:00-07:00", 5, "2026-07-27T10:10:00-07:00")]
    [InlineData("2026-07-27T10:09:59-07:00", 10, "2026-07-27T10:10:00-07:00")]
    public void GetNextBoundary_returns_next_exclusive_local_boundary(
        string now,
        int intervalMinutes,
        string expected)
    {
        var clock = new MutableClock(DateTimeOffset.Parse(now));
        var scheduler = new RefreshScheduler(clock, new AdvancingDelay(clock));

        var boundary = scheduler.GetNextBoundary(intervalMinutes);

        Assert.Equal(DateTimeOffset.Parse(expected), boundary);
    }

    [Fact]
    public async Task WaitForNextBoundary_waits_only_until_aligned_time()
    {
        var clock = new MutableClock(
            DateTimeOffset.Parse("2026-07-27T10:02:17-07:00"));
        var delay = new AdvancingDelay(clock);
        var scheduler = new RefreshScheduler(clock, delay);

        await scheduler.WaitForNextBoundaryAsync(5, CancellationToken.None);

        Assert.Equal([TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(43)], delay.Delays);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-27T10:05:00-07:00"),
            clock.Now);
    }

    [Fact]
    public async Task RunRound_retries_at_ten_and_thirty_seconds_from_initial_attempt()
    {
        var clock = new MutableClock(
            DateTimeOffset.Parse("2026-07-27T10:01:00-07:00"));
        var delay = new AdvancingDelay(clock);
        var scheduler = new RefreshScheduler(clock, delay);
        var attempts = 0;

        var succeeded = await scheduler.RunRoundAsync(
            5,
            _ => Task.FromResult(++attempts >= 3),
            CancellationToken.None);

        Assert.True(succeeded);
        Assert.Equal(3, attempts);
        Assert.Equal(
            [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)],
            delay.Delays);
    }

    [Fact]
    public async Task RunRound_skips_retry_at_or_after_next_boundary()
    {
        var clock = new MutableClock(
            DateTimeOffset.Parse("2026-07-27T10:04:45-07:00"));
        var delay = new AdvancingDelay(clock);
        var scheduler = new RefreshScheduler(clock, delay);
        var attempts = 0;

        var succeeded = await scheduler.RunRoundAsync(
            5,
            _ =>
            {
                attempts++;
                return Task.FromResult(false);
            },
            CancellationToken.None);

        Assert.False(succeeded);
        Assert.Equal(2, attempts);
        Assert.Equal([TimeSpan.FromSeconds(10)], delay.Delays);
    }

    [Fact]
    public async Task RunRound_success_on_initial_attempt_does_not_delay()
    {
        var clock = new MutableClock(
            DateTimeOffset.Parse("2026-07-27T10:01:00-07:00"));
        var delay = new AdvancingDelay(clock);
        var scheduler = new RefreshScheduler(clock, delay);

        var succeeded = await scheduler.RunRoundAsync(
            5,
            _ => Task.FromResult(true),
            CancellationToken.None);

        Assert.True(succeeded);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public void GetNextBoundary_rejects_unsupported_interval()
    {
        var clock = new MutableClock(
            DateTimeOffset.Parse("2026-07-27T10:01:00-07:00"));
        var scheduler = new RefreshScheduler(clock, new AdvancingDelay(clock));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.GetNextBoundary(7));
    }

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; set; } = now;

        public TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
    }

    private sealed class AdvancingDelay(MutableClock clock) : IAsyncDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            clock.Now += delay;
            return Task.CompletedTask;
        }
    }
}
