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

    [Fact]
    public async Task Cancelled_renderer_result_is_not_returned_after_new_request()
    {
        var renderer = new BlockingRenderer();
        using var useCase = new BuildPreviewUseCase(renderer);
        var first = useCase.ExecuteAsync(
            "first.jpg",
            ProcessingRequest.Default,
            800,
            600,
            CancellationToken.None);
        await renderer.FirstStarted.Task;

        var second = useCase.ExecuteAsync(
            "second.jpg",
            ProcessingRequest.Default,
            800,
            600,
            CancellationToken.None);
        renderer.ReleaseFirst.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(new PixelSize(320, 200), (await second).Size);
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

    private sealed class BlockingRenderer : IImagePreviewRenderer
    {
        public TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<PreviewImage> RenderAsync(
            string sourcePath,
            ProcessingRequest request,
            int maximumWidth,
            int maximumHeight,
            CancellationToken cancellationToken)
        {
            if (sourcePath == "first.jpg")
            {
                FirstStarted.SetResult();
                await ReleaseFirst.Task;
            }

            return new PreviewImage([1], new PixelSize(320, 200));
        }
    }
}
