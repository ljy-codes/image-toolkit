using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Domain.Tests;

public sealed class InterfaceContractTests
{
    [Fact]
    public void Domain_interfaces_can_be_implemented_without_framework_dependencies()
    {
        IImageProcessor imageProcessor = new FakeImageProcessor();
        IOutputPathResolver pathResolver = new FakeOutputPathResolver();
        IAtomicFileWriter fileWriter = new FakeAtomicFileWriter();
        IConfigurationStore<FakeConfiguration> configurationStore = new FakeConfigurationStore();
        IImageMetadataReader metadataReader = new FakeMetadataReader();
        IBackgroundRemovalEngine backgroundRemoval = new FakeBackgroundRemovalEngine();
        IAiModelManager modelManager = new FakeAiModelManager();

        Assert.NotNull(imageProcessor);
        Assert.NotNull(pathResolver);
        Assert.NotNull(fileWriter);
        Assert.NotNull(configurationStore);
        Assert.NotNull(metadataReader);
        Assert.NotNull(backgroundRemoval);
        Assert.NotNull(modelManager);
    }

    private sealed class FakeImageProcessor : IImageProcessor
    {
        public Task<ImageProcessingResult> ProcessAsync(
            string sourcePath,
            string outputPath,
            ProcessingRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ImageProcessingResult.Completed(sourcePath, outputPath, 1));
    }

    private sealed class FakeOutputPathResolver : IOutputPathResolver
    {
        public string Resolve(string sourcePath, OutputOptions options, string outputExtension) =>
            sourcePath + outputExtension;
    }

    private sealed class FakeAtomicFileWriter : IAtomicFileWriter
    {
        public Task WriteNewAsync(
            string targetPath,
            Func<Stream, Task> write,
            Func<string, Task<bool>> validate,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReplaceAsync(
            string targetPath,
            Func<Stream, Task> write,
            Func<string, Task<bool>> validate,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed record FakeConfiguration;

    private sealed class FakeConfigurationStore : IConfigurationStore<FakeConfiguration>
    {
        public Task<FakeConfiguration> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new FakeConfiguration());

        public Task SaveAsync(
            FakeConfiguration configuration,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeMetadataReader : IImageMetadataReader
    {
        public Task<ImageFileInfo> ReadAsync(
            string sourcePath,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new ImageFileInfo(sourcePath, "image.jpg", ".jpg", 1, new PixelSize(1, 1), false));
    }

    private sealed class FakeBackgroundRemovalEngine : IBackgroundRemovalEngine
    {
        public Task RemoveBackgroundAsync(
            Stream input,
            Stream output,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeAiModelManager : IAiModelManager
    {
        public Task<bool> IsModelAvailableAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task InstallModelAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveModelAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
