using ActivityWatch.CategoryOverlay.Core.Models;
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

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(3039, "50m 39s")]
    [InlineData(3723, "1h 2m 3s")]
    public void FormatElapsedDuration_keeps_second_precision(
        int seconds,
        string expected)
    {
        Assert.Equal(
            expected,
            OverlayText.FormatElapsedDuration(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void FormatElapsedDuration_clamps_negative_values()
    {
        Assert.Equal(
            "0s",
            OverlayText.FormatElapsedDuration(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void FormatElapsedDuration_floors_fractional_seconds_like_web_ui()
    {
        Assert.Equal(
            "50m 39s",
            OverlayText.FormatElapsedDuration(TimeSpan.FromSeconds(3039.99)));
    }

    [Theory]
    [InlineData(ThresholdDirection.Minimum, "MIN")]
    [InlineData(ThresholdDirection.Maximum, "MAX")]
    public void FormatDirection_uses_compact_requirement_labels(
        ThresholdDirection direction,
        string expected)
    {
        Assert.Equal(expected, OverlayText.FormatDirection(direction));
    }
}
