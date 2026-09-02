using ImageToolkit.Application.Batch;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Tests;

public sealed class BatchTaskCoordinatorTests
{
    [Fact]
    public async Task One_failure_does_not_stop_remaining_items()
    {
        var processed = new List<string>();
        var coordinator = new BatchTaskCoordinator(async (item, _, _) =>
        {
            processed.Add(item.SourcePath);
            await Task.Yield();
            return item.SourcePath.EndsWith("bad.jpg", StringComparison.OrdinalIgnoreCase)
                ? ImageProcessingResult.Failed(item.SourcePath, "read.failed", "读取失败")
                : ImageProcessingResult.Completed(item.SourcePath, item.SourcePath + ".out", 100);
        });

        var summary = await coordinator.RunAsync(
            [
                BatchItem.Waiting("good-1.jpg"),
                BatchItem.Waiting("bad.jpg"),
                BatchItem.Waiting("good-2.jpg")
            ],
            ProcessingRequest.Default,
            1,
            null,
            CancellationToken.None);

        Assert.Equal(3, processed.Count);
        Assert.Equal(2, summary.Completed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(BatchRunState.CompletedWithIssues, summary.State);
    }

    [Fact]
    public async Task Unmet_result_is_not_counted_as_failure()
    {
        var coordinator = new BatchTaskCoordinator((item, _, _) =>
            Task.FromResult(ImageProcessingResult.Unmet(
                item.SourcePath,
                item.SourcePath + ".out",
                500,
                new PixelSize(100, 100),
                "未达到目标大小。")));

        var summary = await coordinator.RunAsync(
            [BatchItem.Waiting("image.png")],
            ProcessingRequest.Default,
            1,
            null,
            CancellationToken.None);

        Assert.Equal(1, summary.Unmet);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task Pause_prevents_next_item_until_resume()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var coordinator = new BatchTaskCoordinator(async (item, _, _) =>
        {
            var number = Interlocked.Increment(ref started);
            if (number == 1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
            }

            return ImageProcessingResult.Completed(item.SourcePath, item.SourcePath + ".out", 1);
        });

        var run = coordinator.RunAsync(
            [BatchItem.Waiting("1.jpg"), BatchItem.Waiting("2.jpg")],
            ProcessingRequest.Default,
            1,
            null,
            CancellationToken.None);

        await firstStarted.Task;
        coordinator.Pause();
        releaseFirst.SetResult();
        await Task.Delay(100);
        Assert.Equal(1, Volatile.Read(ref started));

        coordinator.Resume();
        await run;
        Assert.Equal(2, started);
    }

    [Fact]
    public async Task Cancellation_marks_queued_items_cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var coordinator = new BatchTaskCoordinator(async (item, _, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return ImageProcessingResult.Completed(item.SourcePath, item.SourcePath + ".out", 1);
        });

        var summary = await coordinator.RunAsync(
            [
                BatchItem.Waiting("1.jpg"),
                BatchItem.Waiting("2.jpg"),
                BatchItem.Waiting("3.jpg")
            ],
            ProcessingRequest.Default,
            1,
            null,
            cancellation.Token);

        Assert.Equal(3, summary.Cancelled);
        Assert.Equal(BatchRunState.Cancelled, summary.State);
    }

    [Fact]
    public async Task Processing_uses_request_snapshot()
    {
        long observedTarget = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new BatchTaskCoordinator(async (item, request, _) =>
        {
            await release.Task;
            observedTarget = request.Compression.TargetBytes;
            return ImageProcessingResult.Completed(item.SourcePath, item.SourcePath + ".out", 1);
        });
        var draft = ProcessingRequest.Default;

        var run = coordinator.RunAsync(
            [BatchItem.Waiting("1.jpg")],
            draft,
            1,
            null,
            CancellationToken.None);
        draft = draft with
        {
            Compression = draft.Compression with { TargetBytes = 10 }
        };
        release.SetResult();
        await run;

        Assert.Equal(1_048_576, observedTarget);
        Assert.Equal(10, draft.Compression.TargetBytes);
    }

    [Fact]
    public async Task Rejects_second_run_while_active()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new BatchTaskCoordinator(async (item, _, _) =>
        {
            await release.Task;
            return ImageProcessingResult.Completed(item.SourcePath, item.SourcePath + ".out", 1);
        });
        var firstRun = coordinator.RunAsync(
            [BatchItem.Waiting("1.jpg")],
            ProcessingRequest.Default,
            1,
            null,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.RunAsync(
                [BatchItem.Waiting("2.jpg")],
                ProcessingRequest.Default,
                1,
                null,
                CancellationToken.None));

        release.SetResult();
        await firstRun;
    }
}
