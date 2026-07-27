using System.Windows;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Windows.ViewModels;

namespace ActivityWatch.CategoryOverlay.Windows.Views;

public partial class SettingsWindow : Window
{
    private readonly OverlayConfiguration _configuration;
    private readonly Func<OverlayConfiguration, Task> _save;
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(
        OverlayConfiguration configuration,
        IReadOnlyList<IReadOnlyList<string>> availableCategories,
        Func<OverlayConfiguration, Task> save)
    {
        InitializeComponent();
        _configuration = configuration;
        _save = save;
        _viewModel = new SettingsViewModel(configuration, availableCategories);
        DataContext = _viewModel;
    }

    private void Add_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (AvailableList.SelectedItem is CategoryChoice choice)
        {
            _viewModel.Add(choice);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (TargetList.SelectedItem is TargetEditor target)
        {
            _viewModel.Remove(target);
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (TargetList.SelectedItem is TargetEditor target)
        {
            _viewModel.Move(target, -1);
            TargetList.SelectedItem = target;
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (TargetList.SelectedItem is TargetEditor target)
        {
            _viewModel.Move(target, 1);
            TargetList.SelectedItem = target;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!_viewModel.TryBuild(_configuration, out var updated))
        {
            return;
        }

        try
        {
            await _save(updated);
            Close();
        }
        catch (Exception exception)
        {
            _viewModel.SetValidationMessage(
                $"Could not save settings: {exception.Message}");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        Close();
    }
}
