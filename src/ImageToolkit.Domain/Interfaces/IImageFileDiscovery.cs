using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IImageFileDiscovery
{
    Task<ImageImportResult> DiscoverAsync(
        IEnumerable<string> inputPaths,
        bool includeSubdirectories,
        CancellationToken cancellationToken);
}
