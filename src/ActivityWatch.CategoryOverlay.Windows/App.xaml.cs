using System.IO;
using System.Net.Http;
using ActivityWatch.CategoryOverlay.Core.Abstractions;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;
using ActivityWatch.CategoryOverlay.Windows.Services;
using ActivityWatch.CategoryOverlay.Windows.ViewModels;
using ActivityWatch.CategoryOverlay.Windows.Views;

namespace ActivityWatch.CategoryOverlay.Windows;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly AutostartService _autostartService = new();
    private readonly IClock _clock = new SystemClock();
    private readonly IAsyncDelay _delay = new SystemAsyncDelay();
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private HttpClient? _httpClient;
    private ConfigurationStore? _configurationStore;
    private OverlayConfiguration _configuration = OverlayConfiguration.Default;
    private OverlayState _currentState = OverlayState.Empty;
    private OverlayStateService? _stateService;
    private RefreshScheduler? _refreshScheduler;
    private OverlayViewModel? _overlayViewModel;
    private OverlayWindow? _overlayWindow;
    private SettingsWindow? _settingsWindow;
    private TrayService? _trayService;
    private CancellationTokenSource? _refreshLoopCancellation;
    private bool _isEditMode;
    private bool _isExiting;

    protected override async void OnStartup(System.Windows.StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            "Local\\ActivityWatch.CategoryOverlay",
            out _ownsSingleInstanceMutex);
        if (!_ownsSingleInstanceMutex)
        {
            Shutdown();
            return;
        }

        var configurationPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ActivityWatch",
            "CategoryOverlay",
            "config.json");
        _configurationStore = new ConfigurationStore(configurationPath);
        _configuration = await _configurationStore.LoadAsync(_shutdown.Token);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_configuration.ServerUrl),
            Timeout = TimeSpan.FromSeconds(20),
        };
        _stateService = new OverlayStateService(
            new ActivityWatchClient(_httpClient),
            _clock,
            _configuration);
        _refreshScheduler = new RefreshScheduler(_clock, _delay);
        _overlayViewModel = new OverlayViewModel();
        _overlayWindow = new OverlayWindow(
            _overlayViewModel,
            _configuration,
            SavePositionAsync);
        _trayService = new TrayService(
            ToggleVisibilityAsync,
            () => RefreshWithRetryAsync(_shutdown.Token),
            ToggleEditMode,
            OpenSettings,
            ToggleAutostartAsync,
            ExitApplication);

        if (_configuration.IsVisible)
        {
            _overlayWindow.Show();
        }

        ApplyAutostart(_configuration.StartWithWindows);
        UpdateTray();
        await RefreshWithRetryAsync(_shutdown.Token);
        RestartRefreshLoop();

        if (_configuration.Targets.Count == 0)
        {
            OpenSettings();
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs eventArgs)
    {
        _shutdown.Cancel();
        _refreshLoopCancellation?.Cancel();
        _refreshLoopCancellation?.Dispose();
        _trayService?.Dispose();
        _httpClient?.Dispose();
        _refreshGate.Dispose();
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();
        _shutdown.Dispose();
        base.OnExit(eventArgs);
    }

    private async Task<bool> RefreshOnceAsync(CancellationToken cancellationToken)
    {
        if (_stateService is null || _overlayViewModel is null)
        {
            return false;
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            _currentState = await _stateService.RefreshAsync(cancellationToken);
            await Dispatcher.InvokeAsync(
                () => _overlayViewModel.Apply(_currentState, _configuration.Opacity));
            return !_currentState.IsStale;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private Task<bool> RefreshWithRetryAsync(CancellationToken cancellationToken)
    {
        if (_refreshScheduler is null)
        {
            return Task.FromResult(false);
        }

        return _refreshScheduler.RunRoundAsync(
            _configuration.RefreshMinutes,
            RefreshOnceAsync,
            cancellationToken);
    }

    private async Task SavePositionAsync(double left, double top)
    {
        if (_configurationStore is null)
        {
            return;
        }

        _configuration = _configuration with { Left = left, Top = top };
        _stateService?.UpdateConfiguration(_configuration);
        await _configurationStore.SaveAsync(_configuration, _shutdown.Token);
    }

    private void RestartRefreshLoop()
    {
        _refreshLoopCancellation?.Cancel();
        _refreshLoopCancellation?.Dispose();
        _refreshLoopCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        _ = RunRefreshLoopAsync(_refreshLoopCancellation.Token);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        if (_refreshScheduler is null)
        {
            return;
        }

        try
        {
            while (true)
            {
                await _refreshScheduler.WaitForNextBoundaryAsync(
                    _configuration.RefreshMinutes,
                    cancellationToken);
                await RefreshWithRetryAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ToggleVisibilityAsync()
    {
        if (_overlayWindow is null || _configurationStore is null)
        {
            return;
        }

        var show = !_overlayWindow.IsVisible;
        if (show)
        {
            _overlayWindow.Show();
        }
        else
        {
            _overlayWindow.Hide();
            _isEditMode = false;
        }

        _configuration = _configuration with { IsVisible = show };
        await _configurationStore.SaveAsync(_configuration, _shutdown.Token);
        UpdateTray();
    }

    private void ToggleEditMode()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        if (!_overlayWindow.IsVisible)
        {
            _overlayWindow.Show();
            _configuration = _configuration with { IsVisible = true };
        }

        _isEditMode = !_isEditMode;
        _overlayWindow.SetEditMode(_isEditMode);
        UpdateTray();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(
            _configuration,
            _currentState.AvailableCategories,
            ApplySettingsAsync);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async Task ApplySettingsAsync(OverlayConfiguration updated)
    {
        if (_configurationStore is null)
        {
            return;
        }

        _configuration = updated;
        _stateService?.UpdateConfiguration(updated);
        await _configurationStore.SaveAsync(updated, _shutdown.Token);
        ApplyAutostart(updated.StartWithWindows);
        _overlayViewModel?.Apply(_currentState, updated.Opacity);
        RestartRefreshLoop();
        UpdateTray();
        await RefreshWithRetryAsync(_shutdown.Token);
    }

    private async Task ToggleAutostartAsync()
    {
        if (_configurationStore is null)
        {
            return;
        }

        var enabled = !_autostartService.IsEnabled();
        ApplyAutostart(enabled);
        _configuration = _configuration with { StartWithWindows = enabled };
        await _configurationStore.SaveAsync(_configuration, _shutdown.Token);
        UpdateTray();
    }

    private void ApplyAutostart(bool enabled)
    {
        try
        {
            _autostartService.SetEnabled(enabled);
        }
        catch (UnauthorizedAccessException)
        {
            _configuration = _configuration with
            {
                StartWithWindows = _autostartService.IsEnabled(),
            };
        }
    }

    private void UpdateTray()
    {
        _trayService?.Update(
            _overlayWindow?.IsVisible == true,
            _isEditMode,
            _autostartService.IsEnabled());
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _shutdown.Cancel();
        _refreshLoopCancellation?.Cancel();
        _settingsWindow?.Close();
        _overlayWindow?.Close();
        Shutdown();
    }
}
