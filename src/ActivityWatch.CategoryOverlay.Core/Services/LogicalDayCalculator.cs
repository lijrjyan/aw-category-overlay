namespace ActivityWatch.CategoryOverlay.Core.Services;

public readonly record struct LogicalDayWindow(DateTimeOffset Start, DateTimeOffset End);

public static class LogicalDayCalculator
{
    public static LogicalDayWindow GetWindow(
        DateTimeOffset now,
        TimeOnly startOfDay,
        TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var date = localNow.TimeOfDay < startOfDay.ToTimeSpan()
            ? localNow.Date.AddDays(-1)
            : localNow.Date;
        var startLocal = DateTime.SpecifyKind(
            date + startOfDay.ToTimeSpan(),
            DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        var start = new DateTimeOffset(startLocal, timeZone.GetUtcOffset(startLocal));
        var end = new DateTimeOffset(endLocal, timeZone.GetUtcOffset(endLocal));
        return new LogicalDayWindow(start, end);
    }
}
