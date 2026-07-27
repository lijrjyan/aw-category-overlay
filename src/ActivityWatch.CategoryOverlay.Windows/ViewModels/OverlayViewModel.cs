using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Windows.ViewModels;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private string _lastRefreshText = "--:--";
    private double _headerOpacity = 0.62;
    private double _overlayOpacity = 0.72;

    public ObservableCollection<OverlayRowViewModel> Rows { get; } = [];

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Apply(OverlayState state, double opacity)
    {
        Rows.Clear();
        foreach (var row in state.Rows)
        {
            Rows.Add(OverlayRowViewModel.From(row));
        }

        LastRefreshText = state.LastSuccessfulRefresh?.ToLocalTime().ToString("HH:mm")
            ?? "--:--";
        HeaderOpacity = state.IsStale ? 0.35 : 0.62;
        OverlayOpacity = Math.Clamp(opacity, 0.35, 1.0);
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
            OverlayText.FormatDuration(row.Actual),
            row.FillFraction,
            brush);
    }
}
