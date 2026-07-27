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

        var now = clock.Now;
        var candidate = now
            .AddTicks(-(now.Ticks % TimeSpan.TicksPerMinute))
            .AddMinutes(1);
        for (var minute = 0; minute <= intervalMinutes; minute++)
        {
            var local = TimeZoneInfo.ConvertTime(candidate, clock.LocalTimeZone);
            if (local.Minute % intervalMinutes == 0)
            {
                return local;
            }

            candidate = candidate.AddMinutes(1);
        }

        throw new InvalidOperationException("No aligned refresh boundary found.");
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
        if (await TryRefreshAsync(refresh, cancellationToken))
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

            if (await TryRefreshAsync(refresh, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> TryRefreshAsync(
        Func<CancellationToken, Task<bool>> refresh,
        CancellationToken cancellationToken)
    {
        try
        {
            return await refresh(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
