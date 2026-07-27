using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class OverlayTextTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(57, "57m")]
    [InlineData(60, "1h 00m")]
    [InlineData(91, "1h 31m")]
    [InlineData(601, "10h 01m")]
    public void FormatDuration_returns_compact_text(int minutes, string expected)
    {
        Assert.Equal(
            expected,
            OverlayText.FormatDuration(TimeSpan.FromMinutes(minutes)));
    }
}
