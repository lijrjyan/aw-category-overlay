using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

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
        var sharedCount = Math.Min(Rows.Count, state.Rows.Count);
        for (var index = 0; index < sharedCount; index++)
        {
            Rows[index].Apply(state.Rows[index]);
        }

        for (var index = sharedCount; index < state.Rows.Count; index++)
        {
            Rows.Add(OverlayRowViewModel.From(state.Rows[index]));
        }

        while (Rows.Count > state.Rows.Count)
        {
            Rows.RemoveAt(Rows.Count - 1);
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

public sealed class OverlayRowViewModel : INotifyPropertyChanged
{
    private static readonly MediaBrush AttentionBrush =
        CreateBrush(MediaColor.FromRgb(214, 168, 75));
    private static readonly MediaBrush NormalBrush =
        CreateBrush(MediaColor.FromRgb(79, 163, 199));
    private static readonly MediaBrush CompleteBrush =
        CreateBrush(MediaColor.FromRgb(79, 191, 136));
    private static readonly MediaBrush WarningBrush =
        CreateBrush(MediaColor.FromRgb(217, 104, 104));

    private string _displayName = "";
    private string _thresholdText = "";
    private string _requirementText = "";
    private string _actualText = "";
    private double _fillFraction;
    private MediaBrush _fillBrush = MediaBrushes.Transparent;

    private OverlayRowViewModel()
    {
    }

    public string DisplayName
    {
        get => _displayName;
        private set => SetField(ref _displayName, value);
    }

    public string ThresholdText
    {
        get => _thresholdText;
        private set => SetField(ref _thresholdText, value);
    }

    public string RequirementText
    {
        get => _requirementText;
        private set => SetField(ref _requirementText, value);
    }

    public string ActualText
    {
        get => _actualText;
        private set => SetField(ref _actualText, value);
    }

    public double FillFraction
    {
        get => _fillFraction;
        private set => SetField(ref _fillFraction, value);
    }

    public MediaBrush FillBrush
    {
        get => _fillBrush;
        private set => SetField(ref _fillBrush, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static OverlayRowViewModel From(OverlayRowState row)
    {
        var viewModel = new OverlayRowViewModel();
        viewModel.Apply(row);
        return viewModel;
    }

    public void Apply(OverlayRowState row)
    {
        DisplayName = row.Target.DisplayName;
        ThresholdText = OverlayText.FormatDuration(row.Target.Threshold);
        RequirementText = OverlayText.FormatDirection(row.Target.Direction);
        ActualText = OverlayText.FormatDuration(row.Actual);
        FillFraction = row.FillFraction;
        FillBrush = row.Status switch
        {
            ThresholdStatus.Attention => AttentionBrush,
            ThresholdStatus.Normal => NormalBrush,
            ThresholdStatus.Complete => CompleteBrush,
            ThresholdStatus.Warning => WarningBrush,
            _ => throw new ArgumentOutOfRangeException(nameof(row)),
        };
    }

    private static MediaBrush CreateBrush(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
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
