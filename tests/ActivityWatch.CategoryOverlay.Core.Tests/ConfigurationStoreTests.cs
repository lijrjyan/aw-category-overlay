using ActivityWatch.CategoryOverlay.Core.Models;
using ActivityWatch.CategoryOverlay.Core.Services;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class ConfigurationStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"aw-overlay-{Guid.NewGuid():N}");

    [Fact]
    public async Task Save_then_load_round_trips_configuration()
    {
        var path = Path.Combine(_directory, "config.json");
        var store = new ConfigurationStore(path);
        var config = OverlayConfiguration.Default with
        {
            RefreshMinutes = 10,
            Targets =
            [
                new(
                    ["Work", "Research"],
                    ThresholdDirection.Minimum,
                    TimeSpan.FromHours(2),
                    0),
            ],
        };

        await store.SaveAsync(config, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(config.RefreshMinutes, loaded.RefreshMinutes);
        Assert.Equal(config.ServerUrl, loaded.ServerUrl);
        Assert.Collection(
            loaded.Targets,
            target =>
            {
                Assert.Equal(config.Targets[0].Path, target.Path);
                Assert.Equal(config.Targets[0].Direction, target.Direction);
                Assert.Equal(config.Targets[0].Threshold, target.Threshold);
                Assert.Equal(config.Targets[0].Order, target.Order);
            });
    }

    [Fact]
    public async Task Load_backs_up_corrupt_json_and_returns_defaults()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "config.json");
        await File.WriteAllTextAsync(path, "{broken");
        var store = new ConfigurationStore(path);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(OverlayConfiguration.Default, loaded);
        Assert.Single(Directory.GetFiles(_directory, "config.corrupt-*.json"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Save_rejects_unsupported_refresh_interval(int refreshMinutes)
    {
        var store = new ConfigurationStore(Path.Combine(_directory, "config.json"));
        var config = OverlayConfiguration.Default with { RefreshMinutes = refreshMinutes };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.SaveAsync(config, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
