using System.Collections.Concurrent;
using System.Threading.Channels;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Batch;

public sealed class BatchTaskCoordinator
{
    private readonly Func<
        BatchItem,
        ProcessingRequest,
        CancellationToken,
        Task<ImageProcessingResult>> _process;
    private readonly AsyncPauseGate _pauseGate = new();
    private int _isRunning;
    private BatchRunState _state = BatchRunState.Idle;

    public BatchTaskCoordinator(
        Func<
            BatchItem,
            ProcessingRequest,
            CancellationToken,
            Task<ImageProcessingResult>> process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public BatchRunState State => _state;

    public void Pause()
    {
        if (Volatile.Read(ref _isRunning) == 0)
        {
            return;
        }

        _pauseGate.Pause();
        _state = BatchRunState.Paused;
    }

    public void Resume()
    {
        _pauseGate.Resume();
        if (Volatile.Read(ref _isRunning) != 0)
        {
            _state = BatchRunState.Running;
        }
    }

    public async Task<BatchSummary> RunAsync(
        IEnumerable<BatchItem> items,
        ProcessingRequest request,
        int workerCount,
        IProgress<BatchItem>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(request);

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            throw new InvalidOperationException("已有批处理任务正在运行。");
        }

        var batchItems = items.ToArray();
        var requestSnapshot = CloneRequest(request);
        var concurrency = ResolveWorkerCount(workerCount);
        var results = new ConcurrentBag<ImageProcessingResult>();
        var completedPaths = new ConcurrentDictionary<string, byte>(
            StringComparer.OrdinalIgnoreCase);
        _pauseGate.Resume();
        _state = BatchRunState.Running;

        try
        {
            var channel = Channel.CreateUnbounded<BatchItem>(
                new UnboundedChannelOptions
                {
                    SingleWriter = true,
                    SingleReader = concurrency == 1
                });

            foreach (var item in batchItems)
            {
                await channel.Writer.WriteAsync(item, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            channel.Writer.Complete();
            var workers = Enumerable
                .Range(0, concurrency)
                .Select(_ => RunWorkerAsync(
                    channel.Reader,
                    requestSnapshot,
                    results,
                    completedPaths,
                    progress,
                    cancellationToken))
                .ToArray();

            await Task.WhenAll(workers).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                foreach (var item in batchItems.Where(
                             item => !completedPaths.ContainsKey(item.SourcePath)))
                {
                    var result = ImageProcessingResult.Cancelled(item.SourcePath);
                    results.Add(result);
                    completedPaths.TryAdd(item.SourcePath, 0);
                    progress?.Report(ToBatchItem(item.SourcePath, result));
                }
            }

            var summary = BuildSummary(batchItems.Length, results, cancellationToken);
            _state = summary.State;
            return summary;
        }
        finally
        {
            _pauseGate.Resume();
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    private async Task RunWorkerAsync(
        ChannelReader<BatchItem> reader,
        ProcessingRequest request,
        ConcurrentBag<ImageProcessingResult> results,
        ConcurrentDictionary<string, byte> completedPaths,
        IProgress<BatchItem>? progress,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await _pauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!reader.TryRead(out var item))
            {
                continue;
            }

            progress?.Report(item with { Status = BatchItemStatus.Processing });
            ImageProcessingResult result;
            try
            {
                result = await _process(item, request, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result = ImageProcessingResult.Cancelled(item.SourcePath);
            }
            catch (Exception exception)
            {
                result = ImageProcessingResult.Failed(
                    item.SourcePath,
                    "processing.unexpected",
                    exception.Message);
            }

            results.Add(result);
            completedPaths.TryAdd(item.SourcePath, 0);
            progress?.Report(ToBatchItem(item.SourcePath, result));
        }
    }

    private static BatchSummary BuildSummary(
        int total,
        IEnumerable<ImageProcessingResult> results,
        CancellationToken cancellationToken)
    {
        var resultArray = results.ToArray();
        var completed = resultArray.Count(
            result => result.Status == ImageProcessingStatus.Completed);
        var unmet = resultArray.Count(
            result => result.Status == ImageProcessingStatus.Unmet);
        var failed = resultArray.Count(
            result => result.Status == ImageProcessingStatus.Failed);
        var explicitlyCancelled = resultArray.Count(
            result => result.Status == ImageProcessingStatus.Cancelled);
        var cancelled = explicitlyCancelled + Math.Max(0, total - resultArray.Length);

        var state = cancellationToken.IsCancellationRequested || cancelled > 0
            ? BatchRunState.Cancelled
            : failed > 0 || unmet > 0
                ? BatchRunState.CompletedWithIssues
                : BatchRunState.Completed;

        return new BatchSummary(state, total, completed, unmet, failed, cancelled);
    }

    private static BatchItem ToBatchItem(
        string sourcePath,
        ImageProcessingResult result)
    {
        var status = result.Status switch
        {
            ImageProcessingStatus.Completed => BatchItemStatus.Completed,
            ImageProcessingStatus.Unmet => BatchItemStatus.Unmet,
            ImageProcessingStatus.Failed => BatchItemStatus.Failed,
            _ => BatchItemStatus.Cancelled
        };

        return new BatchItem(sourcePath, status, result);
    }

    private static int ResolveWorkerCount(int workerCount) =>
        workerCount switch
        {
            0 => Math.Clamp(Environment.ProcessorCount / 2, 1, 2),
            1 or 2 or 4 => workerCount,
            _ => throw new ArgumentOutOfRangeException(
                nameof(workerCount),
                "并发数只能是自动、1、2 或 4。")
        };

    private static ProcessingRequest CloneRequest(ProcessingRequest request) =>
        request with
        {
            Compression = request.Compression with { },
            Resize = request.Resize with { },
            AspectRatio = request.AspectRatio with { },
            AiBackgroundRemoval = request.AiBackgroundRemoval with { },
            Background = request.Background with { },
            Metadata = request.Metadata with { },
            Output = request.Output with { }
        };
}
