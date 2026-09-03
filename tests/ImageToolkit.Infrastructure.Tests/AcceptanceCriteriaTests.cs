using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using ImageMagick;
using ImageToolkit.Application.Batch;
using ImageToolkit.Application.Preview;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Files;
using ImageToolkit.Infrastructure.Imaging;
using Xunit.Abstractions;

namespace ImageToolkit.Infrastructure.Tests;

[Collection("AcceptanceCriteria")]
public sealed class AcceptanceCriteriaTests : IDisposable
{
    private const long OneMebibyte = 1024 * 1024;
    private readonly ITestOutputHelper _output;
    private readonly string _temporaryDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "ImageToolkitAcceptance",
            Guid.NewGuid().ToString("N"));

    public AcceptanceCriteriaTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "UserAssets")]
    [Trait("Category", "Acceptance")]
    public async Task User_png_around_2_15_mib_never_keeps_an_over_limit_result()
    {
        var sourceAsset = FindClosestPng(2.15 * OneMebibyte);
        Directory.CreateDirectory(_temporaryDirectory);
        var source = Path.Combine(_temporaryDirectory, Path.GetFileName(sourceAsset));
        File.Copy(sourceAsset, source);
        var originalHash = ComputeHash(source);
        var sourceSize = new FileInfo(source).Length;
        Assert.InRange(sourceSize, 2 * OneMebibyte, 2.3 * OneMebibyte);

        var result = await CreatePipeline().ProcessAsync(
            source,
            ProcessingRequest.Default,
            CancellationToken.None);

        _output.WriteLine(
            $"source={sourceSize}; status={result.Status}; " +
            $"output={result.OutputSizeBytes}; message={result.Message}");
        Assert.Equal(originalHash, ComputeHash(source));
        if (result.Status == ImageProcessingStatus.Completed)
        {
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(new FileInfo(result.OutputPath).Length <= OneMebibyte);
        }
        else
        {
            Assert.Equal(ImageProcessingStatus.Unmet, result.Status);
            Assert.Null(result.OutputPath);
            Assert.False(File.Exists(
                Path.Combine(
                    _temporaryDirectory,
                    Path.GetFileNameWithoutExtension(source) +
                    "-已处理" +
                    Path.GetExtension(source))));
            Assert.Equal(OneMebibyte, result.Diagnostic?.TargetBytes);
            Assert.True(result.Diagnostic?.BestAttemptBytes > OneMebibyte);
            Assert.Contains("未生成输出文件", result.Diagnostic?.UserMessage);
            Assert.Contains("原图保持不变", result.Diagnostic?.UserMessage);
            Assert.NotEmpty(result.Diagnostic?.Suggestions ?? []);
        }
    }

    [Fact]
    [Trait("Category", "UserAssets")]
    [Trait("Category", "Acceptance")]
    public async Task Real_folder_batch_preserves_structure_and_archives_clear_failures()
    {
        var sourceRoot = Path.Combine(_temporaryDirectory, "图集");
        var nested = Path.Combine(sourceRoot, "子目录");
        Directory.CreateDirectory(nested);
        var validSource = Path.Combine(nested, "风景.png");
        File.Copy(FindClosestPng(2.15 * OneMebibyte), validSource);
        var invalidSource = Path.Combine(nested, "损坏图片.png");
        await File.WriteAllTextAsync(invalidSource, "not an image");
        var validHash = ComputeHash(validSource);
        var invalidHash = ComputeHash(invalidSource);
        var discovery = await new ImageFileDiscovery().DiscoverAsync(
            [sourceRoot],
            true,
            CancellationToken.None);
        Assert.Equal(2, discovery.Entries.Count);

        using var archiver = new FailedItemArchiver();
        var pipeline = CreatePipeline(archiver);
        var results = new List<ImageProcessingResult>();
        foreach (var entry in discovery.Entries)
        {
            results.Add(await pipeline.ProcessAsync(
                entry,
                ProcessingRequest.Default,
                CancellationToken.None));
        }

        var validResult = Assert.Single(
            results,
            result => result.SourcePath == validSource);
        Assert.Equal(ImageProcessingStatus.Completed, validResult.Status);
        Assert.Equal(
            Path.Combine(
                _temporaryDirectory,
                "图集-已处理",
                "子目录",
                "风景.png"),
            validResult.OutputPath);
        Assert.True(new FileInfo(validResult.OutputPath!).Length <= OneMebibyte);

        var invalidResult = Assert.Single(
            results,
            result => result.SourcePath == invalidSource);
        Assert.Equal(ImageProcessingStatus.Failed, invalidResult.Status);
        Assert.Null(invalidResult.OutputPath);
        Assert.NotNull(invalidResult.Diagnostic);
        Assert.Contains("未生成输出文件", invalidResult.Message);
        var failedRoot = Path.Combine(_temporaryDirectory, "图集-未处理");
        Assert.True(File.Exists(
            Path.Combine(failedRoot, "子目录", "损坏图片.png")));
        var textReport = await File.ReadAllTextAsync(
            Path.Combine(failedRoot, "失败原因.txt"));
        var csvReport = await File.ReadAllTextAsync(
            Path.Combine(failedRoot, "失败原因.csv"));
        Assert.Contains("失败阶段", textReport);
        Assert.Contains("失败原因", textReport);
        Assert.Contains("处理建议", textReport);
        Assert.Contains("源文件", csvReport);
        Assert.Equal(validHash, ComputeHash(validSource));
        Assert.Equal(invalidHash, ComputeHash(invalidSource));

        var duplicate = await pipeline.ProcessAsync(
            ImageImportEntry.FromFolder(sourceRoot, validSource),
            ProcessingRequest.Default,
            CancellationToken.None);
        Assert.Equal(ImageProcessingStatus.Completed, duplicate.Status);
        Assert.EndsWith(
            Path.Combine("子目录", "风景-2.png"),
            duplicate.OutputPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "UserAssets")]
    [Trait("Category", "Acceptance")]
    public async Task Multi_image_batch_finishes_without_hanging_or_empty_outputs()
    {
        var entries = CreateRepeatedFolderAssets("压力图集", 3);
        var outputRoot = Path.Combine(_temporaryDirectory, "压力输出");
        var request = ProcessingRequest.Default with
        {
            Output = ProcessingRequest.Default.Output with
            {
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = outputRoot
            }
        };
        var pipeline = CreatePipeline();
        var lookup = entries.ToDictionary(
            entry => entry.SourcePath,
            StringComparer.OrdinalIgnoreCase);
        var results = new ConcurrentDictionary<string, ImageProcessingResult>(
            StringComparer.OrdinalIgnoreCase);
        var coordinator = new BatchTaskCoordinator(
            async (item, snapshot, token) =>
            {
                var result = await pipeline.ProcessAsync(
                    lookup[item.SourcePath],
                    snapshot,
                    token);
                results[item.SourcePath] = result;
                return result;
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var stopwatch = Stopwatch.StartNew();

        var summary = await coordinator.RunAsync(
            entries.Select(entry => BatchItem.Waiting(entry.SourcePath)),
            request,
            2,
            null,
            timeout.Token);
        stopwatch.Stop();

        _output.WriteLine(
            $"total={summary.Total}; completed={summary.Completed}; " +
            $"unmet={summary.Unmet}; failed={summary.Failed}; " +
            $"cancelled={summary.Cancelled}; elapsed={stopwatch.Elapsed}");
        Assert.False(timeout.IsCancellationRequested);
        Assert.Equal(entries.Count, summary.Total);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.Cancelled);
        Assert.Equal(entries.Count, results.Count);
        foreach (var result in results.Values)
        {
            if (result.Status == ImageProcessingStatus.Completed)
            {
                Assert.NotNull(result.OutputPath);
                Assert.True(File.Exists(result.OutputPath));
                Assert.InRange(new FileInfo(result.OutputPath).Length, 1, OneMebibyte);
                using var image = new MagickImage(result.OutputPath);
                Assert.True(image.Width > 0 && image.Height > 0);
            }
            else
            {
                Assert.Equal(ImageProcessingStatus.Unmet, result.Status);
                Assert.Null(result.OutputPath);
            }
        }
    }

    [Fact]
    [Trait("Category", "UserAssets")]
    [Trait("Category", "Acceptance")]
    public async Task Real_batch_cancellation_returns_promptly_without_broken_files()
    {
        var entries = CreateRepeatedFolderAssets("取消图集", 4);
        var outputRoot = Path.Combine(_temporaryDirectory, "取消输出");
        var request = ProcessingRequest.Default with
        {
            Output = ProcessingRequest.Default.Output with
            {
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = outputRoot
            }
        };
        var pipeline = CreatePipeline();
        var lookup = entries.ToDictionary(
            entry => entry.SourcePath,
            StringComparer.OrdinalIgnoreCase);
        var coordinator = new BatchTaskCoordinator(
            (item, snapshot, token) =>
                pipeline.ProcessAsync(lookup[item.SourcePath], snapshot, token));
        using var cancellation = new CancellationTokenSource();
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new InlineProgress<BatchItem>(item =>
        {
            if (item.Status == BatchItemStatus.Processing)
            {
                firstStarted.TrySetResult();
            }
        });
        var stopwatch = Stopwatch.StartNew();
        var run = coordinator.RunAsync(
            entries.Select(entry => BatchItem.Waiting(entry.SourcePath)),
            request,
            2,
            progress,
            cancellation.Token);

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(50);
        cancellation.Cancel();
        var summary = await run.WaitAsync(TimeSpan.FromSeconds(30));
        stopwatch.Stop();

        _output.WriteLine(
            $"cancelled={summary.Cancelled}; completed={summary.Completed}; " +
            $"elapsed={stopwatch.Elapsed}");
        Assert.Equal(BatchRunState.Cancelled, summary.State);
        Assert.True(summary.Cancelled > 0);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
        if (Directory.Exists(outputRoot))
        {
            foreach (var file in Directory.EnumerateFiles(
                         outputRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                Assert.True(new FileInfo(file).Length > 0);
                using var image = new MagickImage(file);
                Assert.True(image.Width > 0 && image.Height > 0);
            }
        }
    }

    [Fact]
    [Trait("Category", "UserAssets")]
    [Trait("Category", "Acceptance")]
    public async Task Rapid_preview_requests_only_publish_the_latest_real_image()
    {
        var source = Directory
            .EnumerateFiles(FindUserImageDirectory())
            .Where(IsSupported)
            .OrderByDescending(path => new FileInfo(path).Length)
            .First();
        using var useCase = new BuildPreviewUseCase(new MagickPreviewRenderer());
        var tasks = new List<Task<PreviewImage>>();
        for (var index = 0; index < 12; index++)
        {
            tasks.Add(useCase.ExecuteAsync(
                source,
                ProcessingRequest.Default,
                800 - index * 10,
                600 - index * 10,
                CancellationToken.None));
            await Task.Delay(20);
        }

        var cancelled = 0;
        for (var index = 0; index < tasks.Count - 1; index++)
        {
            try
            {
                await tasks[index];
            }
            catch (OperationCanceledException)
            {
                cancelled++;
            }
        }

        var latest = await tasks[^1];
        _output.WriteLine(
            $"cancelled={cancelled}; latest={latest.Size.Width}x{latest.Size.Height}");
        Assert.Equal(tasks.Count - 1, cancelled);
        Assert.True(latest.Bytes.Length > 0);
        Assert.InRange(latest.Size.Width, 1, 690);
        Assert.InRange(latest.Size.Height, 1, 490);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, true);
        }
    }

    private static ImageProcessingPipeline CreatePipeline(
        FailedItemArchiver? archiver = null) =>
        new(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            new MagickImageProcessor(new AtomicFileWriter()),
            archiver);

    private IReadOnlyList<ImageImportEntry> CreateRepeatedFolderAssets(
        string folderName,
        int repetitions)
    {
        var sourceRoot = Path.Combine(_temporaryDirectory, folderName);
        var assets = Directory
            .EnumerateFiles(FindUserImageDirectory())
            .Where(IsSupported)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(assets);
        for (var repetition = 1; repetition <= repetitions; repetition++)
        {
            var child = Path.Combine(sourceRoot, $"批次-{repetition:00}");
            Directory.CreateDirectory(child);
            foreach (var asset in assets)
            {
                File.Copy(
                    asset,
                    Path.Combine(child, Path.GetFileName(asset)));
            }
        }

        return Directory
            .EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(IsSupported)
            .Select(path => ImageImportEntry.FromFolder(sourceRoot, path))
            .ToArray();
    }

    private static string FindClosestPng(double targetBytes) =>
        Directory
            .EnumerateFiles(FindUserImageDirectory(), "*.png")
            .OrderBy(path => Math.Abs(new FileInfo(path).Length - targetBytes))
            .First();

    private static string FindUserImageDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "测试图");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("未找到开发验收目录“测试图”。");
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsSupported(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".tif" or ".tiff";

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

[CollectionDefinition("AcceptanceCriteria", DisableParallelization = true)]
public sealed class AcceptanceCriteriaCollection;
