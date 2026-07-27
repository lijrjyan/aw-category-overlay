using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class ThresholdEvaluatorTests
{
    private static readonly string[] Research = ["Work", "Research"];

    [Theory]
    [InlineData(ThresholdDirection.Minimum, 30, ThresholdStatus.Attention)]
    [InlineData(ThresholdDirection.Minimum, 60, ThresholdStatus.Complete)]
    [InlineData(ThresholdDirection.Maximum, 30, ThresholdStatus.Normal)]
    [InlineData(ThresholdDirection.Maximum, 61, ThresholdStatus.Warning)]
    public void Evaluate_assigns_status(
        ThresholdDirection direction,
        int actualMinutes,
        ThresholdStatus expected)
    {
        var target = new CategoryTarget(Research, direction, TimeSpan.FromHours(1), 0);

        var row = ThresholdEvaluator.Evaluate(target, TimeSpan.FromMinutes(actualMinutes));

        Assert.Equal(expected, row.Status);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(30, 0.325)]
    [InlineData(60, 0.65)]
    [InlineData(90, 0.825)]
    [InlineData(120, 1.0)]
    [InlineData(240, 1.0)]
    public void Evaluate_maps_progress_around_fixed_divider(int actualMinutes, double expected)
    {
        var target = new CategoryTarget(
            Research,
            ThresholdDirection.Minimum,
            TimeSpan.FromHours(1),
            0);

        var row = ThresholdEvaluator.Evaluate(target, TimeSpan.FromMinutes(actualMinutes));

        Assert.Equal(expected, row.FillFraction, precision: 3);
        Assert.Equal(0.65, row.DividerFraction);
    }

    [Fact]
    public void Evaluate_rejects_non_positive_threshold()
    {
        var target = new CategoryTarget(
            Research,
            ThresholdDirection.Minimum,
            TimeSpan.Zero,
            0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ThresholdEvaluator.Evaluate(target, TimeSpan.Zero));
    }
}
