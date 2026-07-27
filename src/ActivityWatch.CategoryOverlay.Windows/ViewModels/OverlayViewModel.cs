using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Windows.ViewModels;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private string _totalDurationText = "0m";
    private string _lastRefreshText = "--:--";
    private double _headerOpacity = 0.62;
    private double _overlayOpacity = 0.72;
    private int _barFontSize = 16;

    public ObservableCollection<OverlayRowViewModel> Rows { get; } = [];

    public string TotalDurationText
    {
        get => _totalDurationText;
        private set => SetField(ref _totalDurationText, value);
    }

    public string LastRefreshText
    {
        get => _lastRefreshText;
        private set => SetField(ref _lastRefreshText, value);
    }

    public double HeaderOpacity
    {
        get => _headerOpacity;
        private set => SetField(ref _headerOpacity, value);
    }

    public double OverlayOpacity
    {
        get => _overlayOpacity;
        private set => SetField(ref _overlayOpacity, value);
    }

    public int BarFontSize
    {
        get => _barFontSize;
        private set => SetField(ref _barFontSize, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Apply(OverlayState state, double opacity, int barFontSize)
    {
        Rows.Clear();
        foreach (var row in state.Rows)
        {
            Rows.Add(OverlayRowViewModel.From(row));
        }

        TotalDurationText = OverlayText.FormatDuration(state.TotalActiveTime);
        LastRefreshText = state.LastSuccessfulRefresh?.ToLocalTime().ToString("HH:mm")
            ?? "--:--";
        HeaderOpacity = state.IsStale ? 0.35 : 0.62;
        OverlayOpacity = Math.Clamp(opacity, 0.35, 1.0);
        BarFontSize = Math.Clamp(barFontSize, 10, 24);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record OverlayRowViewModel(
    string DisplayName,
    string ThresholdText,
    string RequirementText,
    string ActualText,
    double FillFraction,
    System.Windows.Media.Brush FillBrush)
{
    public static OverlayRowViewModel From(OverlayRowState row)
    {
        var brush = new SolidColorBrush(row.Status switch
        {
            ThresholdStatus.Attention => System.Windows.Media.Color.FromRgb(214, 168, 75),
            ThresholdStatus.Normal => System.Windows.Media.Color.FromRgb(79, 163, 199),
            ThresholdStatus.Complete => System.Windows.Media.Color.FromRgb(79, 191, 136),
            ThresholdStatus.Warning => System.Windows.Media.Color.FromRgb(217, 104, 104),
            _ => throw new ArgumentOutOfRangeException(nameof(row)),
        });
        brush.Freeze();

        return new OverlayRowViewModel(
            row.Target.DisplayName,
            OverlayText.FormatDuration(row.Target.Threshold),
            OverlayText.FormatDirection(row.Target.Direction),
            OverlayText.FormatDuration(row.Actual),
            row.FillFraction,
            brush);
    }
}
