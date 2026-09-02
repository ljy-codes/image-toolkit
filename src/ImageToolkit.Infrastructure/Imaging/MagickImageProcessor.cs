using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class MagickImageProcessor : IImageProcessor
{
    private readonly IAtomicFileWriter _fileWriter;
    private readonly MagickMetadataProcessor _metadata = new();
    private readonly MagickCompressionEncoder _encoder = new();

    public MagickImageProcessor(IAtomicFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
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

        using var image = new MagickImage(sourcePath);
        _metadata.ApplyInputOrientation(image);
        ApplyAspectRatio(image, request.AspectRatio, request.Background);
        ApplyResize(image, request.Resize);

        var format = ResolveFormat(outputPath);
        ResolveTransparency(image, format, request.Background);
        _metadata.ApplyOutputMetadata(image, request.Metadata);
        var encoded = await _encoder.EncodeAsync(
            image,
            format,
            request,
            cancellationToken).ConfigureAwait(false);

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

        if (!encoded.ReachedTarget)
        {
            return new ImageProcessingResult(
                sourcePath,
                outputPath,
                ImageProcessingStatus.Unmet,
                encoded.Bytes.LongLength,
                encoded.FinalSize,
                encoded.Quality,
                encoded.UsedAutomaticResize,
                encoded.UsedPngQuantization,
                "compression.target-unmet",
                "已达到画质或尺寸下限，仍未达到目标文件大小。");
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

    private static void ApplyAspectRatio(
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

    private static void ApplyResize(MagickImage image, ResizeOptions options)
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
        if (format != OutputImageFormat.Jpeg || !image.HasAlpha)
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
