using ActivityWatch.CategoryOverlay.Core.Models;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public static class OverlayText
{
    public static string FormatDuration(TimeSpan duration)
    {
        var totalMinutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        if (totalMinutes < 60)
        {
            return $"{totalMinutes}m";
        }

        return $"{totalMinutes / 60}h {totalMinutes % 60:00}m";
    }

    public static string FormatDirection(ThresholdDirection direction)
    {
        return direction switch
        {
            ThresholdDirection.Minimum => "MIN",
            ThresholdDirection.Maximum => "MAX",
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }
}
