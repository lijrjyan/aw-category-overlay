namespace ActivityWatch.CategoryOverlay.Core.Models;

public enum ThresholdStatus
{
    Attention,
    Normal,
    Complete,
    Warning,
}

public sealed record OverlayRowState(
    CategoryTarget Target,
    TimeSpan Actual,
    ThresholdStatus Status,
    double FillFraction,
    double DividerFraction = 0.65);

