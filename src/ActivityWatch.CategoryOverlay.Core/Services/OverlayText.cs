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

    public static string FormatElapsedDuration(TimeSpan duration)
    {
        var totalSeconds = Math.Max(0, (long)Math.Floor(duration.TotalSeconds));
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours:00}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
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
