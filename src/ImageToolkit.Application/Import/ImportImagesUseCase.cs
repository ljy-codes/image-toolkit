using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Import;

public sealed class ImportImagesUseCase
{
    private readonly IImageFileDiscovery _discovery;

    public ImportImagesUseCase(IImageFileDiscovery discovery)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    public Task<ImageImportResult> ExecuteAsync(
        IEnumerable<string> inputPaths,
        bool includeSubdirectories,
        CancellationToken cancellationToken) =>
        _discovery.DiscoverAsync(
            inputPaths,
            includeSubdirectories,
            cancellationToken);
}
