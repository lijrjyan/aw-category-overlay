using System.Windows;
using System.Windows.Input;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Windows.Interop;
using ActivityWatch.CategoryOverlay.Windows.ViewModels;

namespace ActivityWatch.CategoryOverlay.Windows.Views;

public partial class OverlayWindow : Window
{
    private readonly Func<double, double, Task> _savePosition;
    private bool _isEditMode;

    public OverlayWindow(
        OverlayViewModel viewModel,
        OverlayConfiguration configuration,
        Func<double, double, Task> savePosition)
    {
        InitializeComponent();
        DataContext = viewModel;
        _savePosition = savePosition;
        SourceInitialized += (_, _) => ClickThroughWindow.Apply(this, enabled: true);
        Loaded += (_, _) => RestorePosition(configuration);
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    public void SetEditMode(bool enabled)
    {
        _isEditMode = enabled;
        ClickThroughWindow.Apply(this, enabled: !enabled);
        if (!enabled)
        {
            _ = _savePosition(Left, Top);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (_isEditMode && eventArgs.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void RestorePosition(OverlayConfiguration configuration)
    {
        var workArea = SystemParameters.WorkArea;
        var desiredLeft = configuration.Left ?? workArea.Right - ActualWidth - 16;
        var desiredTop = configuration.Top;
        Left = Math.Clamp(desiredLeft, workArea.Left, workArea.Right - ActualWidth);
        Top = Math.Clamp(desiredTop, workArea.Top, workArea.Bottom - ActualHeight);
    }
}
