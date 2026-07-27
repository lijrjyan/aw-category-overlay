namespace ActivityWatch.CategoryOverlay.Core.Abstractions;

public interface IClock
{
    DateTimeOffset Now { get; }

    TimeZoneInfo LocalTimeZone { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
}
