using ActivityWatch.CategoryOverlay.Core.Models;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public static class ThresholdEvaluator
{
    private const double Divider = 0.65;

    public static OverlayRowState Evaluate(CategoryTarget target, TimeSpan actual)
    {
        if (target.Threshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(target),
                "Threshold must be positive.");
        }

        var ratio = Math.Max(0, actual.TotalSeconds / target.Threshold.TotalSeconds);
        var fill = ratio <= 1
            ? ratio * Divider
            : Divider + (Math.Min(ratio - 1, 1) * (1 - Divider));

        var status = target.Direction switch
        {
            ThresholdDirection.Minimum when actual < target.Threshold =>
                ThresholdStatus.Attention,
            ThresholdDirection.Minimum => ThresholdStatus.Complete,
            ThresholdDirection.Maximum when actual > target.Threshold =>
                ThresholdStatus.Warning,
            ThresholdDirection.Maximum => ThresholdStatus.Normal,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

        return new OverlayRowState(target, actual, status, fill);
    }
}
