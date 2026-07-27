using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class LogicalDayCalculatorTests
{
    private static readonly TimeZoneInfo Pacific =
        TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    [Theory]
    [InlineData("2026-07-27T03:59:00-07:00", "2026-07-26T04:00:00-07:00")]
    [InlineData("2026-07-27T04:00:00-07:00", "2026-07-27T04:00:00-07:00")]
    [InlineData("2026-07-27T18:00:00-07:00", "2026-07-27T04:00:00-07:00")]
    public void GetWindow_uses_configured_0400_boundary(string nowText, string startText)
    {
        var window = LogicalDayCalculator.GetWindow(
            DateTimeOffset.Parse(nowText),
            new TimeOnly(4, 0),
            Pacific);

        Assert.Equal(DateTimeOffset.Parse(startText), window.Start);
        Assert.Equal(window.Start.AddDays(1), window.End);
    }
}

