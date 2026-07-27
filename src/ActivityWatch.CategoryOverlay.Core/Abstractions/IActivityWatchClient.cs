using ActivityWatch.CategoryOverlay.Core.Models;

namespace ActivityWatch.CategoryOverlay.Core.Abstractions;

public interface IActivityWatchClient
{
    Task<ActivityWatchSnapshot> GetSnapshotAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken);
}
