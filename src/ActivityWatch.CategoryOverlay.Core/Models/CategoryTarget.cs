namespace ActivityWatch.CategoryOverlay.Core.Models;

public enum ThresholdDirection
{
    Minimum,
    Maximum,
}

public sealed record CategoryTarget(
    IReadOnlyList<string> Path,
    ThresholdDirection Direction,
    TimeSpan Threshold,
    int Order)
{
    public string DisplayName => string.Join(" › ", Path);
}
