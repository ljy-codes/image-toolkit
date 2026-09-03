using System.Collections.Concurrent;
using System.Diagnostics;
using ImageMagick;
using ImageToolkit.Application.Batch;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.AI;
using ImageToolkit.Infrastructure.Files;
using ImageToolkit.Infrastructure.Imaging;
using Xunit.Abstractions;

namespace ImageToolkit.Infrastructure.Tests;

[Collection("AcceptanceCriteria")]
public sealed class AiAcceptanceTests
{
    private readonly ITestOutputHelper _output;

    public AiAcceptanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AiAcceptanceTheory]
    [InlineData(
        BackgroundRemovalMode.Portrait,
        "微信图片_20260902102806_8_102.jpg",
        "portrait.png")]
    [InlineData(
        BackgroundRemovalMode.GeneralObject,
        "微信图片_20260902102803_7_102.png",
        "general-object.png")]
    [Trait("Category", "UserAssets")]
    [Trait("Category", "AiAcceptance")]
    public async Task Real_image_cutout_contains_transparent_opaque_and_soft_edges(
        BackgroundRemovalMode mode,
        string sourceFileName,
        string outputFileName)
    {
        var modelDirectory = Environment.GetEnvironmentVariable(
            "IMAGETOOLKIT_AI_MODEL_DIR");
        var evidenceDirectory = Environment.GetEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR");
        Assert.False(string.IsNullOrWhiteSpace(modelDirectory));
        Assert.False(string.IsNullOrWhiteSpace(evidenceDirectory));
        Directory.CreateDirectory(evidenceDirectory);

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        var manager = new LocalAiModelManager(client, modelDirectory);
        var modelId = mode == BackgroundRemovalMode.Portrait
            ? AiModelManifest.PortraitModelId
            : AiModelManifest.GeneralModelId;
        var status = await manager.GetStatusAsync(modelId, CancellationToken.None);
        if (!status.IsInstalled)
        {
            var progress = new Progress<double>(value =>
                _output.WriteLine($"download={mode}; progress={value:F1}%"));
            await manager.InstallModelAsync(
                modelId,
                progress,
                CancellationToken.None);
        }

        var sourcePath = Path.Combine(
            FindUserImageDirectory(),
            sourceFileName);
        Assert.True(File.Exists(sourcePath));
        var outputPath = Path.Combine(evidenceDirectory, outputFileName);
        var engine = new OnnxBackgroundRemovalEngine(manager);
        await using (var input = File.OpenRead(sourcePath))
        await using (var output = File.Create(outputPath))
        {
            await engine.RemoveBackgroundAsync(
                input,
                output,
                mode,
                CancellationToken.None);
        }

        using var source = new MagickImage(sourcePath);
        using var result = new MagickImage(outputPath);
        Assert.Equal(MagickFormat.Png, result.Format);
        Assert.True(result.HasAlpha);
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        var rgba = result.GetPixels().ToByteArray(PixelMapping.RGBA);
        Assert.NotNull(rgba);
        var alpha = rgba
            .Where((_, index) => index % 4 == 3)
            .ToArray();
        var transparentRatio = alpha.Count(value => value <= 16) / (double)alpha.Length;
        var opaqueRatio = alpha.Count(value => value >= 239) / (double)alpha.Length;
        var softEdgeRatio = alpha.Count(value => value is > 16 and < 239) /
                            (double)alpha.Length;
        _output.WriteLine(
            $"mode={mode}; transparent={transparentRatio:P2}; " +
            $"opaque={opaqueRatio:P2}; soft-edge={softEdgeRatio:P2}; " +
            $"output={outputPath}");

        Assert.True(transparentRatio >= 0.05);
        Assert.True(opaqueRatio >= 0.02);
        Assert.True(softEdgeRatio >= 0.001);
    }

    [AiAcceptanceFact]
    [Trait("Category", "UserAssets")]
    [Trait("Category", "AiAcceptance")]
    public async Task Real_ai_batch_finishes_with_valid_transparent_pngs()
    {
        var modelDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_AI_MODEL_DIR");
        var evidenceDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR");
        var sourceDirectory = Path.Combine(evidenceDirectory, "batch-source");
        var outputDirectory = Path.Combine(evidenceDirectory, "batch-output");
        ResetDirectory(sourceDirectory);
        ResetDirectory(outputDirectory);
        var sourceFileNames = new[]
        {
            "微信图片_20260902102756_4_102.png",
            "微信图片_20260902102803_7_102.png",
            "微信图片_20260902102801_6_102.jpg"
        };
        foreach (var fileName in sourceFileNames)
        {
            File.Copy(
                Path.Combine(FindUserImageDirectory(), fileName),
                Path.Combine(sourceDirectory, fileName));
        }

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        var manager = new LocalAiModelManager(client, modelDirectory);
        var status = await manager.GetStatusAsync(
            AiModelManifest.GeneralModelId,
            CancellationToken.None);
        Assert.True(status.IsInstalled);
        using var engine = new OnnxBackgroundRemovalEngine(manager);
        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            new MagickImageProcessor(new AtomicFileWriter(), engine));
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                Enabled = false
            },
            AiBackgroundRemoval = ProcessingRequest.Default.AiBackgroundRemoval with
            {
                Mode = BackgroundRemovalMode.GeneralObject
            },
            Background = ProcessingRequest.Default.Background with
            {
                Mode = BackgroundMode.Transparent
            },
            Output = ProcessingRequest.Default.Output with
            {
                Format = OutputImageFormat.Png,
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = outputDirectory
            }
        };
        var sourcePaths = Directory
            .EnumerateFiles(sourceDirectory)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new ConcurrentDictionary<string, ImageProcessingResult>(
            StringComparer.OrdinalIgnoreCase);
        var coordinator = new BatchTaskCoordinator(
            async (item, snapshot, token) =>
            {
                var result = await pipeline.ProcessAsync(
                    item.SourcePath,
                    snapshot,
                    token);
                results[item.SourcePath] = result;
                return result;
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var started = DateTimeOffset.UtcNow;

        var summary = await coordinator.RunAsync(
            sourcePaths.Select(BatchItem.Waiting),
            request,
            2,
            null,
            timeout.Token);

        _output.WriteLine(
            $"ai-batch total={summary.Total}; completed={summary.Completed}; " +
            $"failed={summary.Failed}; cancelled={summary.Cancelled}; " +
            $"elapsed={DateTimeOffset.UtcNow - started}");
        Assert.False(timeout.IsCancellationRequested);
        Assert.Equal(sourcePaths.Length, summary.Completed);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.Cancelled);
        foreach (var result in results.Values)
        {
            Assert.Equal(ImageProcessingStatus.Completed, result.Status);
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(new FileInfo(result.OutputPath).Length > 0);
            using var image = new MagickImage(result.OutputPath);
            Assert.Equal(MagickFormat.Png, image.Format);
            Assert.True(image.HasAlpha);
        }
    }

    [AiCorpusAcceptanceFact]
    [Trait("Category", "AiCorpusAcceptance")]
    public async Task Real_general_model_processes_curated_diverse_images()
    {
        var modelDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_AI_MODEL_DIR");
        var corpusDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR");
        var evidenceDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR");
        var outputDirectory = Path.Combine(evidenceDirectory, "ai-corpus");
        ResetDirectory(outputDirectory);
        var samples = new[]
        {
            ("people", "007-portrait.jpg"),
            ("group", "014-group.jpg"),
            ("tree", "019-product.jpg"),
            ("gecko", "033-glass.jpg"),
            ("cat", "043-animal.jpg"),
            ("frosted-plant", "041-animal.jpg"),
            ("hoverfly", "049-insect.jpg"),
            ("food", "061-food.jpg"),
            ("building", "066-architecture.jpg"),
            ("night-wheel", "076-low-light.jpg"),
            ("camera", "084-white-object.jpg"),
            ("butterflies", "091-white-object.jpg"),
            ("phone", "093-electronics.jpg"),
            ("wheel-spokes", "098-fine-structure.jpg"),
            ("vehicle-people", "097-vehicle.jpg")
        };
        var failures = new List<string>();

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        var manager = new LocalAiModelManager(client, modelDirectory);
        var status = await manager.GetStatusAsync(
            AiModelManifest.GeneralModelId,
            CancellationToken.None);
        Assert.True(status.IsInstalled);
        using var engine = new OnnxBackgroundRemovalEngine(manager);

        foreach (var sample in samples)
        {
            var sourcePath = Path.Combine(corpusDirectory, sample.Item2);
            Assert.True(File.Exists(sourcePath), $"缺少验收图片：{sample.Item2}");
            var outputPath = Path.Combine(
                outputDirectory,
                $"{sample.Item1}-{Path.GetFileNameWithoutExtension(sourcePath)}.png");
            var stopwatch = Stopwatch.StartNew();
            await using (var input = File.OpenRead(sourcePath))
            await using (var output = File.Create(outputPath))
            {
                await engine.RemoveBackgroundAsync(
                    input,
                    output,
                    BackgroundRemovalMode.GeneralObject,
                    CancellationToken.None);
            }

            stopwatch.Stop();
            using var source = new MagickImage(sourcePath);
            using var result = new MagickImage(outputPath);
            Assert.Equal(source.Width, result.Width);
            Assert.Equal(source.Height, result.Height);
            Assert.True(result.HasAlpha);
            var rgba = result.GetPixels().ToByteArray(PixelMapping.RGBA);
            Assert.NotNull(rgba);
            var alpha = rgba
                .Where((_, index) => index % 4 == 3)
                .ToArray();
            var transparentRatio =
                alpha.Count(value => value <= 16) / (double)alpha.Length;
            var opaqueRatio =
                alpha.Count(value => value >= 239) / (double)alpha.Length;
            _output.WriteLine(
                $"scene={sample.Item1}; file={sample.Item2}; " +
                $"elapsed={stopwatch.Elapsed}; " +
                $"transparent={transparentRatio:P2}; opaque={opaqueRatio:P2}; " +
                $"output={outputPath}");
            if (transparentRatio < 0.01)
            {
                failures.Add($"{sample.Item1} 没有形成有效透明背景");
            }

            if (opaqueRatio < 0.01)
            {
                failures.Add($"{sample.Item1} 没有保留有效前景");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"多场景 AI 验收失败：{string.Join("；", failures)}。");
    }

    [AiCorpusAcceptanceFact]
    [Trait("Category", "AiCorpusAcceptance")]
    public async Task Ambiguous_landscape_is_rejected_with_clear_reason()
    {
        var modelDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_AI_MODEL_DIR");
        var corpusDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR");
        var evidenceDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR");
        var outputDirectory = Path.Combine(
            evidenceDirectory,
            "ai-ambiguous-landscape");
        ResetDirectory(outputDirectory);

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        var manager = new LocalAiModelManager(client, modelDirectory);
        using var engine = new OnnxBackgroundRemovalEngine(manager);
        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            new MagickImageProcessor(new AtomicFileWriter(), engine));
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                Enabled = false
            },
            AiBackgroundRemoval = ProcessingRequest.Default.AiBackgroundRemoval with
            {
                Mode = BackgroundRemovalMode.GeneralObject
            },
            Background = ProcessingRequest.Default.Background with
            {
                Mode = BackgroundMode.Transparent
            },
            Output = ProcessingRequest.Default.Output with
            {
                Format = OutputImageFormat.Png,
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = outputDirectory
            }
        };
        var sourcePath = Path.Combine(corpusDirectory, "095-landscape.jpg");

        var result = await pipeline.ProcessAsync(
            sourcePath,
            request,
            CancellationToken.None);

        Assert.Equal(ImageProcessingStatus.Failed, result.Status);
        Assert.Equal("ai.subject-not-found", result.ErrorCode);
        Assert.Null(result.OutputPath);
        Assert.Contains("未识别到明确主体", result.Diagnostic?.UserMessage);
        Assert.Contains("全景", result.Diagnostic?.UserMessage);
        Assert.Contains("未生成输出文件", result.Diagnostic?.UserMessage);
        Assert.Empty(Directory.EnumerateFiles(outputDirectory));
    }

    [AiCorpusAcceptanceFact]
    [Trait("Category", "AiPostprocessingAcceptance")]
    public async Task Annotation_cleanup_preserves_real_fine_subjects()
    {
        var modelDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_AI_MODEL_DIR");
        var corpusDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR");
        var evidenceDirectory = GetRequiredEnvironmentVariable(
            "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR");
        var outputDirectory = Path.Combine(
            evidenceDirectory,
            "ai-postprocessing");
        ResetDirectory(outputDirectory);
        var samples = new[]
        {
            ("butterflies", "091-white-object.jpg", 15),
            ("hoverfly", "049-insect.jpg", 2),
            ("wheel-spokes", "098-fine-structure.jpg", 2)
        };

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        var manager = new LocalAiModelManager(client, modelDirectory);
        using var engine = new OnnxBackgroundRemovalEngine(manager);

        foreach (var sample in samples)
        {
            var sourcePath = Path.Combine(corpusDirectory, sample.Item2);
            var outputPath = Path.Combine(
                outputDirectory,
                $"{sample.Item1}-{Path.GetFileNameWithoutExtension(sourcePath)}.png");
            await using (var input = File.OpenRead(sourcePath))
            await using (var output = File.Create(outputPath))
            {
                await engine.RemoveBackgroundAsync(
                    input,
                    output,
                    BackgroundRemovalMode.GeneralObject,
                    CancellationToken.None);
            }

            using var result = new MagickImage(outputPath);
            var componentSizes = GetForegroundComponentSizes(result);
            _output.WriteLine(
                $"scene={sample.Item1}; components={componentSizes.Count}; " +
                $"largest={string.Join(",", componentSizes.Take(15))}");
            Assert.InRange(componentSizes.Count, 1, sample.Item3);
        }
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(value));
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

    private static IReadOnlyList<int> GetForegroundComponentSizes(
        MagickImage image)
    {
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        var rgba = image.GetPixels().ToByteArray(PixelMapping.RGBA)!;
        var foreground = new bool[width * height];
        for (var index = 0; index < foreground.Length; index++)
        {
            foreground[index] = rgba[index * 4 + 3] >= 128;
        }

        var visited = new bool[foreground.Length];
        var queue = new Queue<int>();
        var sizes = new List<int>();
        for (var start = 0; start < foreground.Length; start++)
        {
            if (!foreground[start] || visited[start])
            {
                continue;
            }

            queue.Clear();
            queue.Enqueue(start);
            visited[start] = true;
            var size = 0;
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                size++;
                var x = index % width;
                var y = index / width;
                Visit(index - 1, x > 0);
                Visit(index + 1, x < width - 1);
                Visit(index - width, y > 0);
                Visit(index + width, y < height - 1);
            }

            sizes.Add(size);
        }

        sizes.Sort((left, right) => right.CompareTo(left));
        return sizes;

        void Visit(int index, bool valid)
        {
            if (!valid || visited[index] || !foreground[index])
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }
    }

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
}
