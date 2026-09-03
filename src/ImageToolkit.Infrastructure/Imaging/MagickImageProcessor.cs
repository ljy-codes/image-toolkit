using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class MagickImageProcessor : IImageProcessor
{
    private readonly IAtomicFileWriter _fileWriter;
    private readonly IBackgroundRemovalEngine? _backgroundRemovalEngine;
    private readonly MagickMetadataProcessor _metadata = new();
    private readonly MagickCompressionEncoder _encoder = new();

    public MagickImageProcessor(
        IAtomicFileWriter fileWriter,
        IBackgroundRemovalEngine? backgroundRemovalEngine = null)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
        _backgroundRemovalEngine = backgroundRemovalEngine;
    }

    public async Task<ImageProcessingResult> ProcessAsync(
        string sourcePath,
        string outputPath,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureOverwriteIsSafe(sourcePath, request);
        using var sourceImage = new MagickImage(sourcePath);
        _metadata.ApplyInputOrientation(sourceImage);
        MagickImage? aiImage = null;
        var image = sourceImage;
        if (request.AiBackgroundRemoval.Mode != BackgroundRemovalMode.Disabled)
        {
            aiImage = await RemoveBackgroundAsync(
                sourceImage,
                request.AiBackgroundRemoval.Mode,
                cancellationToken).ConfigureAwait(false);
            image = aiImage;
        }

        try
        {
            ApplyAspectRatio(image, request.AspectRatio, request.Background);
            ApplyResize(image, request.Resize);

            var format = request.Output.Format == OutputImageFormat.Original
                ? ResolveFormat(outputPath)
                : request.Output.Format;
            ResolveTransparency(image, format, request.Background);
            _metadata.ApplyOutputMetadata(image, request.Metadata);
            var encoded = await _encoder.EncodeAsync(
                image,
                format,
                request,
                cancellationToken).ConfigureAwait(false);

            if (!encoded.ReachedTarget)
            {
                DeleteEmptyReservation(outputPath);
                var targetText = FormatFileSize(request.Compression.TargetBytes);
                var bestText = FormatFileSize(encoded.Bytes.LongLength);
                var message =
                    $"未达到目标大小。目标为 {targetText}，最低可达 {bestText}。本次未生成输出文件，原图保持不变。";
                return ImageProcessingResult.Unmet(
                    sourcePath,
                    encoded.Bytes.LongLength,
                    encoded.FinalSize,
                    message,
                    new ProcessingDiagnostic(
                        "压缩",
                        message,
                        "编码搜索已达到当前允许的画质、尺寸或格式下限。",
                        request.Compression.TargetBytes,
                        encoded.Bytes.LongLength,
                        BuildCompressionSuggestions(format, request)),
                    encoded.Quality,
                    encoded.UsedAutomaticResize,
                    encoded.UsedPngQuantization);
            }

            Func<Stream, Task> write = stream =>
                stream.WriteAsync(encoded.Bytes, CancellationToken.None).AsTask();
            Func<string, Task<bool>> validate = ValidateOutputAsync;

            if (request.Output.Mode == OutputMode.OverwriteOriginal)
            {
                await _fileWriter.ReplaceAsync(
                    outputPath,
                    write,
                    validate,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _fileWriter.WriteNewAsync(
                    outputPath,
                    write,
                    validate,
                    cancellationToken).ConfigureAwait(false);
            }

            return ImageProcessingResult.Completed(
                sourcePath,
                outputPath,
                encoded.Bytes.LongLength,
                encoded.FinalSize,
                encoded.Quality,
                encoded.UsedAutomaticResize,
                encoded.UsedPngQuantization);
        }
        finally
        {
            aiImage?.Dispose();
        }
    }

    private async Task<MagickImage> RemoveBackgroundAsync(
        MagickImage image,
        BackgroundRemovalMode mode,
        CancellationToken cancellationToken)
    {
        if (_backgroundRemovalEngine is null)
        {
            throw new InvalidOperationException(
                "AI 抠图组件未加载，请重新安装应用或关闭 AI 抠图。");
        }

        using var input = new MemoryStream();
        image.Format = MagickFormat.Png;
        image.Write(input);
        input.Position = 0;
        using var output = new MemoryStream();
        try
        {
            await _backgroundRemovalEngine.RemoveBackgroundAsync(
                    input,
                    output,
                    mode,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateAiException(exception);
        }

        output.Position = 0;
        try
        {
            return new MagickImage(output);
        }
        catch (Exception exception)
        {
            throw CreateAiException(exception);
        }
    }

    private static ImageProcessingStageException CreateAiException(
        Exception exception)
    {
        var modelMissing = exception is FileNotFoundException;
        var subjectNotFound = exception is InvalidDataException &&
            exception.Message.Contains(
                "未识别到",
                StringComparison.Ordinal);
        var message = modelMissing
            ? $"{exception.Message} 本次未生成输出文件，原图保持不变。"
            : $"AI 抠图未完成：{exception.Message} 本次未生成输出文件，原图保持不变。";
        return new ImageProcessingStageException(
            modelMissing
                ? "ai.model-missing"
                : subjectNotFound
                    ? "ai.subject-not-found"
                    : "ai.inference-failed",
            new ProcessingDiagnostic(
                "AI 模型",
                message,
                exception.ToString(),
                null,
                null,
                modelMissing
                    ? ["在“AI 抠图”区域安装对应模型。", "确认模型显示“已安装”后重新处理。"]
                    : subjectNotFound
                        ? ["选择主体明确、与背景区分较明显的图片。", "全景或纹理图片可关闭 AI 抠图后继续处理。"]
                    : ["重新安装对应 AI 模型。", "关闭 AI 抠图可继续执行普通图片处理。"]),
            exception);
    }

    private static IReadOnlyList<string> BuildCompressionSuggestions(
        OutputImageFormat format,
        ProcessingRequest request)
    {
        var suggestions = new List<string>();
        if (!request.Compression.AllowAutomaticResize)
        {
            suggestions.Add("允许自动缩小图片尺寸。");
        }

        if (format == OutputImageFormat.Png &&
            !request.Compression.AllowPngQuantization)
        {
            suggestions.Add("启用 PNG 颜色量化。");
        }

        if (format is OutputImageFormat.Png or OutputImageFormat.Bmp or OutputImageFormat.Tiff)
        {
            suggestions.Add("转换为 JPEG 或 WebP。");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("提高目标文件大小，或手动降低输出尺寸。");
        }

        return suggestions;
    }

    private static string FormatFileSize(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:F2} MB"
            : $"{Math.Max(1, bytes / 1024d):F0} KB";

    private static void DeleteEmptyReservation(string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length == 0)
        {
            File.Delete(path);
        }
    }

    private static void EnsureOverwriteIsSafe(
        string sourcePath,
        ProcessingRequest request)
    {
        if (request.Output.Mode != OutputMode.OverwriteOriginal ||
            ResolveFormat(sourcePath) != OutputImageFormat.Tiff)
        {
            return;
        }

        using var images = new MagickImageCollection(sourcePath);
        if (images.Count > 1)
        {
            throw new InvalidOperationException(
                "多页 TIFF 不支持覆盖原文件，请改用新文件输出。");
        }
    }

    internal static void ApplyAspectRatio(
        MagickImage image,
        AspectRatioOptions options,
        BackgroundOptions background)
    {
        if (options.Mode == AspectRatioMode.Original)
        {
            return;
        }

        var source = new PixelSize((int)image.Width, (int)image.Height);
        if (options.Mode == AspectRatioMode.Crop)
        {
            var crop = MagickGeometryCalculator.CalculateCrop(
                source,
                options.RatioWidth,
                options.RatioHeight,
                options.CropAnchor);
            image.Crop(new MagickGeometry(
                crop.X,
                crop.Y,
                (uint)crop.Width,
                (uint)crop.Height));
            image.ResetPage();
            return;
        }

        var canvas = MagickGeometryCalculator.CalculateCanvas(
            source,
            options.RatioWidth,
            options.RatioHeight);
        image.Extent(
            (uint)canvas.Width,
            (uint)canvas.Height,
            Gravity.Center,
            ResolveBackgroundColor(background));
    }

    internal static void ApplyResize(MagickImage image, ResizeOptions options)
    {
        var current = new PixelSize((int)image.Width, (int)image.Height);
        var target = MagickGeometryCalculator.CalculateResize(current, options);
        if (target != current)
        {
            image.Resize((uint)target.Width, (uint)target.Height);
        }
    }

    private static void ResolveTransparency(
        MagickImage image,
        OutputImageFormat format,
        BackgroundOptions background)
    {
        if (!image.HasAlpha)
        {
            return;
        }

        var usesSolidBackground = background.Mode is
            BackgroundMode.White or
            BackgroundMode.Black or
            BackgroundMode.Custom;
        if (!usesSolidBackground && format != OutputImageFormat.Jpeg)
        {
            return;
        }

        image.BackgroundColor = ResolveBackgroundColor(background);
        image.Alpha(AlphaOption.Remove);
    }

    private static MagickColor ResolveBackgroundColor(BackgroundOptions options) =>
        options.Mode switch
        {
            BackgroundMode.Black => MagickColors.Black,
            BackgroundMode.Transparent => MagickColors.Transparent,
            BackgroundMode.Custom => new MagickColor(options.CustomColor),
            _ => MagickColors.White
        };

    private static OutputImageFormat ResolveFormat(string outputPath) =>
        Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => OutputImageFormat.Jpeg,
            ".png" => OutputImageFormat.Png,
            ".webp" => OutputImageFormat.Webp,
            ".bmp" => OutputImageFormat.Bmp,
            ".tif" or ".tiff" => OutputImageFormat.Tiff,
            _ => throw new NotSupportedException("不支持所选输出格式。")
        };

    private static Task<bool> ValidateOutputAsync(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return Task.FromResult(false);
        }

        try
        {
            using var image = new MagickImage(path);
            return Task.FromResult(image.Width > 0 && image.Height > 0);
        }
        catch (MagickException)
        {
            return Task.FromResult(false);
        }
    }
}
