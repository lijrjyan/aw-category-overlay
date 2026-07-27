using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ActivityWatch.CategoryOverlay.Core.Models;

namespace ActivityWatch.CategoryOverlay.Windows.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private int _refreshMinutes;
    private double _opacity;
    private int _barFontSize;
    private bool _startWithWindows;
    private string _validationMessage = string.Empty;

    public SettingsViewModel(
        OverlayConfiguration configuration,
        IReadOnlyList<IReadOnlyList<string>> availableCategories)
    {
        var selectedPaths = configuration.Targets
            .Select(target => PathKey(target.Path))
            .ToHashSet(StringComparer.Ordinal);
        AvailableCategories = new ObservableCollection<CategoryChoice>(
            availableCategories
                .Where(path => !selectedPaths.Contains(PathKey(path)))
                .OrderBy(path => string.Join(" > ", path), StringComparer.Ordinal)
                .Select(path => new CategoryChoice(path)));
        Targets = new ObservableCollection<TargetEditor>(
            configuration.Targets
                .OrderBy(target => target.Order)
                .Select(target => new TargetEditor(
                    target,
                    availableCategories.Any(
                        path => path.SequenceEqual(target.Path, StringComparer.Ordinal)))));
        _refreshMinutes = configuration.RefreshMinutes;
        _opacity = configuration.Opacity;
        _barFontSize = configuration.BarFontSize;
        _startWithWindows = configuration.StartWithWindows;
    }

    public ObservableCollection<CategoryChoice> AvailableCategories { get; }

    public ObservableCollection<TargetEditor> Targets { get; }

    public IReadOnlyList<ThresholdDirection> Directions { get; } =
        Enum.GetValues<ThresholdDirection>();

    public IReadOnlyList<int> RefreshIntervals { get; } = [5, 10];

    public int RefreshMinutes
    {
        get => _refreshMinutes;
        set => SetField(ref _refreshMinutes, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, value);
    }

    public int BarFontSize
    {
        get => _barFontSize;
        set => SetField(ref _barFontSize, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetField(ref _startWithWindows, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetField(ref _validationMessage, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Add(CategoryChoice choice)
    {
        AvailableCategories.Remove(choice);
        Targets.Add(new TargetEditor(
            new CategoryTarget(
                choice.Path,
                ThresholdDirection.Minimum,
                TimeSpan.FromHours(1),
                Targets.Count),
            isAvailable: true));
    }

    public void Remove(TargetEditor target)
    {
        Targets.Remove(target);
        if (target.IsAvailable)
        {
            AvailableCategories.Add(new CategoryChoice(target.Path));
            SortAvailableCategories();
        }
    }

    public void Move(TargetEditor target, int delta)
    {
        var current = Targets.IndexOf(target);
        var destination = current + delta;
        if (current < 0 || destination < 0 || destination >= Targets.Count)
        {
            return;
        }

        Targets.Move(current, destination);
    }

    public bool TryBuild(
        OverlayConfiguration existing,
        out OverlayConfiguration configuration)
    {
        if (RefreshMinutes is not (5 or 10))
        {
            ValidationMessage = "Refresh interval must be 5 or 10 minutes.";
            configuration = existing;
            return false;
        }

        if (BarFontSize is < 10 or > 24)
        {
            ValidationMessage = "Bar font size must be 10–24.";
            configuration = existing;
            return false;
        }

        var targets = new List<CategoryTarget>();
        for (var index = 0; index < Targets.Count; index++)
        {
            var editor = Targets[index];
            if (!int.TryParse(editor.HoursText, out var hours)
                || !int.TryParse(editor.MinutesText, out var minutes)
                || hours < 0
                || minutes < 0
                || minutes > 59)
            {
                ValidationMessage = "Hours must be non-negative and minutes 0–59.";
                configuration = existing;
                return false;
            }

            var threshold = TimeSpan.FromHours(hours)
                + TimeSpan.FromMinutes(minutes);
            if (threshold <= TimeSpan.Zero)
            {
                ValidationMessage = "Every selected category needs a positive threshold.";
                configuration = existing;
                return false;
            }

            targets.Add(new CategoryTarget(
                editor.Path,
                editor.Direction,
                threshold,
                index));
        }

        ValidationMessage = string.Empty;
        configuration = existing with
        {
            RefreshMinutes = RefreshMinutes,
            Opacity = Math.Clamp(Opacity, 0.35, 1.0),
            BarFontSize = BarFontSize,
            StartWithWindows = StartWithWindows,
            Targets = targets,
        };
        return true;
    }

    public void SetValidationMessage(string message)
    {
        ValidationMessage = message;
    }

    private static string PathKey(IReadOnlyList<string> path)
    {
        return string.Join('\u001f', path);
    }

    private void SortAvailableCategories()
    {
        var sorted = AvailableCategories
            .OrderBy(choice => choice.DisplayName, StringComparer.Ordinal)
            .ToList();
        AvailableCategories.Clear();
        foreach (var choice in sorted)
        {
            AvailableCategories.Add(choice);
        }
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

public sealed record CategoryChoice(IReadOnlyList<string> Path)
{
    public string DisplayName => string.Join(" > ", Path);
}

public sealed class TargetEditor : INotifyPropertyChanged
{
    private ThresholdDirection _direction;
    private string _hoursText;
    private string _minutesText;

    public TargetEditor(CategoryTarget target, bool isAvailable)
    {
        Path = target.Path;
        _direction = target.Direction;
        _hoursText = ((int)target.Threshold.TotalHours).ToString();
        _minutesText = target.Threshold.Minutes.ToString();
        IsAvailable = isAvailable;
    }

    public IReadOnlyList<string> Path { get; }

    public string DisplayName => IsAvailable
        ? string.Join(" > ", Path)
        : $"{string.Join(" > ", Path)} (unavailable)";

    public bool IsAvailable { get; }

    public ThresholdDirection Direction
    {
        get => _direction;
        set => SetField(ref _direction, value);
    }

    public string HoursText
    {
        get => _hoursText;
        set => SetField(ref _hoursText, value);
    }

    public string MinutesText
    {
        get => _minutesText;
        set => SetField(ref _minutesText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
