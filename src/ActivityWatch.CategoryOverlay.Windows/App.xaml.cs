using System.IO;
using System.Net.Http;
using ActivityWatch.CategoryOverlay.Core.Abstractions;
using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;
using ActivityWatch.CategoryOverlay.Windows.ViewModels;
using ActivityWatch.CategoryOverlay.Windows.Views;

namespace ActivityWatch.CategoryOverlay.Windows;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private HttpClient? _httpClient;
    private ConfigurationStore? _configurationStore;
    private OverlayConfiguration _configuration = OverlayConfiguration.Default;
    private OverlayStateService? _stateService;
    private OverlayViewModel? _overlayViewModel;
    private OverlayWindow? _overlayWindow;

    protected override async void OnStartup(System.Windows.StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

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
            new SystemClock(),
            _configuration);
        _overlayViewModel = new OverlayViewModel();
        _overlayWindow = new OverlayWindow(
            _overlayViewModel,
            _configuration,
            SavePositionAsync);

        if (_configuration.IsVisible)
        {
            _overlayWindow.Show();
        }

        await RefreshOnceAsync(_shutdown.Token);
    }

    protected override void OnExit(System.Windows.ExitEventArgs eventArgs)
    {
        _shutdown.Cancel();
        _httpClient?.Dispose();
        _shutdown.Dispose();
        base.OnExit(eventArgs);
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        if (_stateService is null || _overlayViewModel is null)
        {
            return;
        }

        var state = await _stateService.RefreshAsync(cancellationToken);
        _overlayViewModel.Apply(state, _configuration.Opacity);
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
}
