using ImageMagick;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Files;
using ImageToolkit.Infrastructure.Imaging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class MagickImageProcessorTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Applies_orientation_and_resets_output_orientation()
    {
        var source = TestImages.CreateJpeg(
            _directory,
            width: 400,
            height: 200,
            orientation: OrientationType.RightTop);
        var output = ReserveOutput("oriented.jpg");
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(
            source,
            output,
            ProcessingRequest.Default,
            CancellationToken.None);

        using var image = new MagickImage(output);
        Assert.Equal(ImageProcessingStatus.Completed, result.Status);
        Assert.Equal(200u, image.Width);
        Assert.Equal(400u, image.Height);
        Assert.Equal(OrientationType.TopLeft, image.Orientation);
    }

    [Fact]
    public async Task Removes_gps_but_preserves_ordinary_exif()
    {
        var source = TestImages.CreateJpeg(_directory, withExif: true);
        var output = ReserveOutput("metadata.jpg");
        var processor = CreateProcessor();

        await processor.ProcessAsync(
            source,
            output,
            ProcessingRequest.Default,
            CancellationToken.None);

        using var image = new MagickImage(output);
        var profile = image.GetExifProfile();
        Assert.NotNull(profile);
        Assert.NotNull(profile.GetValue(ExifTag.DateTimeOriginal));
        Assert.Null(profile.GetValue(ExifTag.GPSLatitude));
    }

    [Fact]
    public async Task Transparent_jpeg_request_is_written_as_png()
    {
        var source = TestImages.CreateTransparentPng(_directory);
        var request = ProcessingRequest.Default with
        {
            Background = ProcessingRequest.Default.Background with
            {
                Mode = BackgroundMode.Transparent
            },
            Output = ProcessingRequest.Default.Output with
            {
                Format = OutputImageFormat.Jpeg
            }
        };
        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            CreateProcessor());

        var result = await pipeline.ProcessAsync(source, request, CancellationToken.None);

        Assert.Equal(".png", Path.GetExtension(result.OutputPath));
        using var image = new MagickImage(result.OutputPath!);
        Assert.Equal(MagickFormat.Png, image.Format);
        Assert.True(image.HasAlpha);
    }

    [Fact]
    public async Task Manual_dimensions_are_preserved_when_target_is_unmet()
    {
        var source = TestImages.CreateJpeg(_directory, width: 800, height: 600);
        var output = ReserveOutput("manual.jpg");
        var request = ProcessingRequest.Default with
        {
            Resize = ProcessingRequest.Default.Resize with
            {
                Enabled = true,
                Width = 400,
                Height = 300
            },
            Compression = ProcessingRequest.Default.Compression with
            {
                TargetBytes = 1
            }
        };
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(
            source,
            output,
            request,
            CancellationToken.None);

        Assert.Equal(ImageProcessingStatus.Unmet, result.Status);
        Assert.Equal(new PixelSize(400, 300), result.FinalSize);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task StrictTarget_does_not_keep_output_when_target_is_unmet()
    {
        var source = TestImages.CreateJpeg(
            _directory,
            fileName: "strict-source.jpg",
            width: 800,
            height: 600);
        var originalBytes = await File.ReadAllBytesAsync(source);
        var output = ReserveOutput("strict-output.jpg");
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                TargetBytes = 1
            }
        };
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(
            source,
            output,
            request,
            CancellationToken.None);

        Assert.Equal(ImageProcessingStatus.Unmet, result.Status);
        Assert.Null(result.OutputPath);
        Assert.False(File.Exists(output));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(1, result.Diagnostic?.TargetBytes);
        Assert.True(result.Diagnostic?.BestAttemptBytes > 1);
        Assert.Contains("未生成输出文件", result.Diagnostic?.UserMessage);
        Assert.NotEmpty(result.Diagnostic?.Suggestions ?? []);
    }

    [Fact]
    public async Task Pipeline_archives_folder_item_when_target_is_unmet()
    {
        var root = Path.Combine(_directory, "图集");
        Directory.CreateDirectory(root);
        var source = TestImages.CreateJpeg(
            root,
            fileName: "strict-source.jpg",
            width: 800,
            height: 600);
        var entry = ImageImportEntry.FromFolder(root, source);
        var archiver = new RecordingFailedItemArchiver();
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                TargetBytes = 1
            }
        };
        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            CreateProcessor(),
            archiver);

        var result = await pipeline.ProcessAsync(
            entry,
            request,
            CancellationToken.None);

        Assert.Equal(ImageProcessingStatus.Unmet, result.Status);
        Assert.Same(entry, archiver.Entry);
        Assert.Same(result, archiver.Result);
    }

    [Fact]
    public async Task Missing_ai_model_returns_clear_stage_and_does_not_write_output()
    {
        var source = TestImages.CreateJpeg(
            _directory,
            fileName: "ai-source.jpg",
            width: 640,
            height: 480);
        var request = ProcessingRequest.Default with
        {
            AiBackgroundRemoval = ProcessingRequest.Default.AiBackgroundRemoval with
            {
                Mode = BackgroundRemovalMode.Portrait
            },
            Output = ProcessingRequest.Default.Output with
            {
                Format = OutputImageFormat.Png
            }
        };
        var processor = new MagickImageProcessor(
            new AtomicFileWriter(),
            new MissingModelEngine());
        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            processor);

        var result = await pipeline.ProcessAsync(
            source,
            request,
            CancellationToken.None);

        Assert.Equal(ImageProcessingStatus.Failed, result.Status);
        Assert.Equal("ai.model-missing", result.ErrorCode);
        Assert.Equal("AI 模型", result.Diagnostic?.Stage);
        Assert.Contains("尚未安装", result.Diagnostic?.UserMessage);
        Assert.Contains("原图保持不变", result.Diagnostic?.UserMessage);
        Assert.Null(result.OutputPath);
        Assert.NotEmpty(result.Diagnostic?.Suggestions ?? []);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(Path.Combine(_directory, "ai-source-已处理.png")));
    }

    [Fact]
    public async Task Original_tiff_is_written_as_tiff()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "source.tif");
        using (var image = new MagickImage(MagickColors.CornflowerBlue, 320, 240))
        {
            image.Format = MagickFormat.Tiff;
            image.Write(source);
        }

        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            CreateProcessor());

        var result = await pipeline.ProcessAsync(
            source,
            ProcessingRequest.Default,
            CancellationToken.None);

        Assert.Equal(".tif", Path.GetExtension(result.OutputPath));
        using var output = new MagickImage(result.OutputPath!);
        Assert.Equal(MagickFormat.Tiff, output.Format);
    }

    [Fact]
    public async Task Multi_page_tiff_cannot_overwrite_original()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "multi-page.tif");
        using (var images = new MagickImageCollection())
        {
            images.Add(new MagickImage(MagickColors.CornflowerBlue, 320, 240));
            images.Add(new MagickImage(MagickColors.OrangeRed, 320, 240));
            images.Write(source);
        }

        var request = ProcessingRequest.Default with
        {
            Output = ProcessingRequest.Default.Output with
            {
                Mode = OutputMode.OverwriteOriginal
            }
        };
        var processor = CreateProcessor();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(source, source, request, CancellationToken.None));

        Assert.Equal("多页 TIFF 不支持覆盖原文件，请改用新文件输出。", exception.Message);
        using var unchanged = new MagickImageCollection(source);
        Assert.Equal(2, unchanged.Count);
    }

    [Fact]
    public async Task Png_uses_lossless_output_before_optional_quantization()
    {
        using var image = new MagickImage(MagickColors.CornflowerBlue, 400, 300);
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                AllowPngQuantization = true
            }
        };

        var result = await new MagickCompressionEncoder().EncodeAsync(
            image,
            OutputImageFormat.Png,
            request,
            CancellationToken.None);

        Assert.True(result.ReachedTarget);
        Assert.False(result.UsedPngQuantization);
    }

    [Fact]
    public async Task Png_automatically_resizes_when_target_remains_unmet()
    {
        using var image = new MagickImage(MagickColors.CornflowerBlue, 1600, 1200);
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                TargetBytes = 1,
                AllowPngQuantization = false
            }
        };

        var result = await new MagickCompressionEncoder().EncodeAsync(
            image,
            OutputImageFormat.Png,
            request,
            CancellationToken.None);

        Assert.False(result.ReachedTarget);
        Assert.True(result.UsedAutomaticResize);
        Assert.True(result.FinalSize.Width < 1600);
        Assert.True(result.FinalSize.Height < 1200);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private MagickImageProcessor CreateProcessor() =>
        new(new AtomicFileWriter());

    private string ReserveOutput(string fileName)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, fileName);
        using (File.Create(path))
        {
        }

        return path;
    }

    private sealed class RecordingFailedItemArchiver : IFailedItemArchiver
    {
        public ImageImportEntry? Entry { get; private set; }

        public ImageProcessingResult? Result { get; private set; }

        public Task ArchiveAsync(
            ImageImportEntry entry,
            ImageProcessingResult result,
            CancellationToken cancellationToken)
        {
            Entry = entry;
            Result = result;
            return Task.CompletedTask;
        }
    }

    private sealed class MissingModelEngine : IBackgroundRemovalEngine
    {
        public Task RemoveBackgroundAsync(
            Stream input,
            Stream output,
            BackgroundRemovalMode mode,
            CancellationToken cancellationToken) =>
            throw new FileNotFoundException("尚未安装“人像抠图模型”。");
    }
}
