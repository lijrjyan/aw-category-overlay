using ActivityWatch.CategoryOverlay.Core.Abstractions;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public sealed class RefreshScheduler(IClock clock, IAsyncDelay delay)
{
    private static readonly TimeSpan[] RetryOffsets =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    public DateTimeOffset GetNextBoundary(int intervalMinutes)
    {
        if (intervalMinutes is not (5 or 10))
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMinutes));
        }

        var local = TimeZoneInfo.ConvertTime(clock.Now, clock.LocalTimeZone);
        var intervalTicks = TimeSpan.FromMinutes(intervalMinutes).Ticks;
        var elapsedTicks = local.TimeOfDay.Ticks;
        var nextTicks = ((elapsedTicks / intervalTicks) + 1) * intervalTicks;
        var nextLocal = local.Date.AddTicks(nextTicks);
        var offset = clock.LocalTimeZone.GetUtcOffset(nextLocal);
        return new DateTimeOffset(nextLocal, offset);
    }

    public async Task WaitForNextBoundaryAsync(
        int intervalMinutes,
        CancellationToken cancellationToken)
    {
        var wait = GetNextBoundary(intervalMinutes) - clock.Now;
        if (wait > TimeSpan.Zero)
        {
            await delay.DelayAsync(wait, cancellationToken);
        }
    }

    public async Task<bool> RunRoundAsync(
        int intervalMinutes,
        Func<CancellationToken, Task<bool>> refresh,
        CancellationToken cancellationToken)
    {
        var startedAt = clock.Now;
        var boundary = GetNextBoundary(intervalMinutes);
        if (await refresh(cancellationToken))
        {
            return true;
        }

        foreach (var offset in RetryOffsets)
        {
            var retryAt = startedAt + offset;
            if (retryAt >= boundary)
            {
                break;
            }

            var wait = retryAt - clock.Now;
            if (wait > TimeSpan.Zero)
            {
                await delay.DelayAsync(wait, cancellationToken);
            }

            if (await refresh(cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}
