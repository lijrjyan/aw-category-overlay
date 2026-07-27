namespace ActivityWatch.CategoryOverlay.Core.Models;

public sealed record OverlayConfiguration
{
    public int SchemaVersion { get; init; } = 1;

    public string ServerUrl { get; init; } = "http://localhost:5600";

    public int RefreshMinutes { get; init; } = 5;

    public double Opacity { get; init; } = 0.72;

    public double? Left { get; init; }

    public double Top { get; init; } = 16;

    public bool StartWithWindows { get; init; } = true;

    public bool IsVisible { get; init; } = true;

    public IReadOnlyList<CategoryTarget> Targets { get; init; } = [];

    public static OverlayConfiguration Default { get; } = new();
}
