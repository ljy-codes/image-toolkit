using ImageToolkit.Application.Import;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Tests;

public sealed class ImportImagesUseCaseTests
{
    [Fact]
    public async Task Delegates_paths_and_options_to_discovery()
    {
        var discovery = new RecordingDiscovery();
        var useCase = new ImportImagesUseCase(discovery);

        var result = await useCase.ExecuteAsync(
            ["a.jpg", "folder"],
            true,
            CancellationToken.None);

        Assert.Equal(["a.jpg", "folder"], discovery.Paths);
        Assert.True(discovery.IncludeSubdirectories);
        Assert.Equal(["resolved.jpg"], result.Files);
    }

    private sealed class RecordingDiscovery : IImageFileDiscovery
    {
        public IReadOnlyList<string> Paths { get; private set; } = [];

        public bool IncludeSubdirectories { get; private set; }

        public Task<ImageImportResult> DiscoverAsync(
            IEnumerable<string> inputPaths,
            bool includeSubdirectories,
            CancellationToken cancellationToken)
        {
            Paths = inputPaths.ToArray();
            IncludeSubdirectories = includeSubdirectories;
            return Task.FromResult(
                new ImageImportResult(["resolved.jpg"], []));
        }
    }
}
