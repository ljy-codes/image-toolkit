using ImageToolkit.Application.Preview;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Tests;

public sealed class BuildPreviewUseCaseTests
{
    [Fact]
    public async Task New_request_cancels_previous_preview()
    {
        var renderer = new RecordingRenderer();
        using var useCase = new BuildPreviewUseCase(renderer);

        var first = useCase.ExecuteAsync(
            "first.jpg",
            ProcessingRequest.Default,
            800,
            600,
            CancellationToken.None);
        await Task.Delay(50);
        var second = useCase.ExecuteAsync(
            "second.jpg",
            ProcessingRequest.Default,
            800,
            600,
            CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var result = await second;

        Assert.Equal(1, renderer.RenderCount);
        Assert.Equal(new PixelSize(320, 200), result.Size);
        Assert.Equal("second.jpg", renderer.LastSourcePath);
    }

    private sealed class RecordingRenderer : IImagePreviewRenderer
    {
        public int RenderCount { get; private set; }

        public string? LastSourcePath { get; private set; }

        public Task<PreviewImage> RenderAsync(
            string sourcePath,
            ProcessingRequest request,
            int maximumWidth,
            int maximumHeight,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenderCount++;
            LastSourcePath = sourcePath;
            return Task.FromResult(
                new PreviewImage([1, 2, 3], new PixelSize(320, 200)));
        }
    }
}
