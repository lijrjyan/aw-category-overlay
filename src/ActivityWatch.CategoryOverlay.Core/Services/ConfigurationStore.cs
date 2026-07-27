using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityWatch.CategoryOverlay.Core.Models;

namespace ActivityWatch.CategoryOverlay.Core.Services;

public sealed class ConfigurationStore(string path)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<OverlayConfiguration> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return OverlayConfiguration.Default;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<OverlayConfiguration>(
                stream,
                Options,
                cancellationToken) ?? OverlayConfiguration.Default;
        }
        catch (JsonException)
        {
            var backup = Path.Combine(
                Path.GetDirectoryName(path)!,
                $"config.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Move(path, backup, overwrite: false);
            return OverlayConfiguration.Default;
        }
    }

    public async Task SaveAsync(
        OverlayConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (configuration.RefreshMinutes is not (5 or 10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Refresh interval must be 5 or 10 minutes.");
        }

        if (configuration.BarFontSize is < 10 or > 24)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Bar font size must be between 10 and 24.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                configuration,
                Options,
                cancellationToken);
        }

        File.Move(temporary, path, overwrite: true);
    }
}
