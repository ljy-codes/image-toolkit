using System.Collections.Concurrent;
using System.Diagnostics;
using ImageMagick;
using ImageToolkit.Application.Batch;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Files;
using ImageToolkit.Infrastructure.Imaging;
using Xunit.Abstractions;

namespace ImageToolkit.Infrastructure.Tests;

[Collection("AcceptanceCriteria")]
public sealed class CorpusAcceptanceTests
{
    private readonly ITestOutputHelper _output;

    public CorpusAcceptanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CorpusAcceptanceTheory]
    [Trait("Category", "CorpusAcceptance")]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Normal_processing_is_fast_and_valid_at_multiple_batch_sizes(
        int imageCount)
    {
        var sourceDirectory = GetRequiredDirectory(
            "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR");
        var evidenceRoot = GetRequiredDirectory(
            "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR");
        var outputDirectory = Path.Combine(
            evidenceRoot,
            $"normal-{imageCount}");
        ResetDirectory(outputDirectory);
        var sourcePaths = EnumerateImages(sourceDirectory)
            .Take(imageCount)
            .ToArray();
        Assert.Equal(imageCount, sourcePaths.Length);

        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            new MagickImageProcessor(new AtomicFileWriter()));
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                Enabled = false
            },
            Output = ProcessingRequest.Default.Output with
            {
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = outputDirectory
            }
        };
        var results = new ConcurrentBag<ImageProcessingResult>();
        var coordinator = new BatchTaskCoordinator(
            async (item, snapshot, token) =>
            {
                var result = await pipeline.ProcessAsync(
                    item.SourcePath,
                    snapshot,
                    token);
                results.Add(result);
                return result;
            });
        var stopwatch = Stopwatch.StartNew();

        var summary = await coordinator.RunAsync(
            sourcePaths.Select(BatchItem.Waiting),
            request,
            0,
            null,
            CancellationToken.None);

        stopwatch.Stop();
        var averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds /
                                  sourcePaths.Length;
        _output.WriteLine(
            $"normal-{imageCount} total={stopwatch.Elapsed}; " +
            $"average={averageMilliseconds:F1}ms; " +
            $"completed={summary.Completed}; failed={summary.Failed}");
        Assert.Equal(imageCount, summary.Completed);
        Assert.Equal(0, summary.Failed);
        Assert.True(
            averageMilliseconds < 1_000,
            $"普通处理平均耗时 {averageMilliseconds:F1}ms，超过 1000ms 验收线。");
        foreach (var result in results)
        {
            Assert.Equal(ImageProcessingStatus.Completed, result.Status);
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            using var image = new MagickImage(result.OutputPath);
            Assert.True(image.Width > 0 && image.Height > 0);
        }
    }

    [CorpusAcceptanceFact]
    [Trait("Category", "CorpusAcceptance")]
    public async Task Strict_compression_never_keeps_an_oversized_result()
    {
        var sourceDirectory = GetRequiredDirectory(
            "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR");
        var evidenceRoot = GetRequiredDirectory(
            "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR");
        var outputDirectory = Path.Combine(evidenceRoot, "compression-sample");
        ResetDirectory(outputDirectory);
        var sourcePaths = EnumerateImages(sourceDirectory).Take(20).ToArray();
        Assert.Equal(20, sourcePaths.Length);
        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            new MagickImageProcessor(new AtomicFileWriter()));
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                Enabled = true,
                TargetBytes = 1_048_576
            },
            Output = ProcessingRequest.Default.Output with
            {
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = outputDirectory
            }
        };

        foreach (var sourcePath in sourcePaths)
        {
            var result = await pipeline.ProcessAsync(
                sourcePath,
                request,
                CancellationToken.None);
            if (result.Status == ImageProcessingStatus.Completed)
            {
                Assert.NotNull(result.OutputPath);
                Assert.True(new FileInfo(result.OutputPath).Length <= 1_048_576);
                continue;
            }

            Assert.Equal(ImageProcessingStatus.Unmet, result.Status);
            Assert.Null(result.OutputPath);
            Assert.NotNull(result.Diagnostic);
            Assert.Contains("目标", result.Diagnostic.UserMessage);
            Assert.Contains("未生成输出文件", result.Diagnostic.UserMessage);
        }
    }

    private static string[] EnumerateImages(string directory) =>
        Directory
            .EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is
                ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".tif" or ".tiff")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string GetRequiredDirectory(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.True(Directory.Exists(value));
        return value;
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);
    }
}
