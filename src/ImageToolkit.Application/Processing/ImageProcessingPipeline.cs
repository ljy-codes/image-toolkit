using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Processing;

public sealed class ImageProcessingPipeline
{
    private readonly ProcessingRequestValidator _validator;
    private readonly IOutputPathResolver _pathResolver;
    private readonly IImageProcessor _processor;

    public ImageProcessingPipeline(
        ProcessingRequestValidator validator,
        IOutputPathResolver pathResolver,
        IImageProcessor processor)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public async Task<ImageProcessingResult> ProcessAsync(
        string sourcePath,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(request);
        if (!validation.IsValid)
        {
            return ImageProcessingResult.Failed(
                sourcePath,
                validation.Errors[0].Code,
                validation.Errors[0].Message);
        }

        var effectiveFormat = ResolveFormat(sourcePath, request);
        var effectiveRequest = request with
        {
            Output = request.Output with { Format = effectiveFormat }
        };
        var extension = ToExtension(sourcePath, effectiveFormat);
        var outputPath = _pathResolver.Resolve(
            sourcePath,
            effectiveRequest.Output,
            extension);

        try
        {
            return await _processor.ProcessAsync(
                sourcePath,
                outputPath,
                effectiveRequest,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DeleteUnusedReservation(outputPath, effectiveRequest);
            return ImageProcessingResult.Cancelled(sourcePath);
        }
        catch (Exception exception)
        {
            DeleteUnusedReservation(outputPath, effectiveRequest);
            return ImageProcessingResult.Failed(
                sourcePath,
                "processing.failed",
                exception.Message);
        }
    }

    private static OutputImageFormat ResolveFormat(
        string sourcePath,
        ProcessingRequest request)
    {
        if (request.Background.Mode == BackgroundMode.Transparent &&
            request.Output.Format == OutputImageFormat.Jpeg)
        {
            return OutputImageFormat.Png;
        }

        if (request.Output.Format != OutputImageFormat.Original)
        {
            return request.Output.Format;
        }

        var sourceFormat = Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => OutputImageFormat.Jpeg,
            ".png" => OutputImageFormat.Png,
            ".webp" => OutputImageFormat.Webp,
            ".bmp" => OutputImageFormat.Bmp,
            ".tif" or ".tiff" => OutputImageFormat.Tiff,
            _ => OutputImageFormat.Jpeg
        };

        return request.Background.Mode == BackgroundMode.Transparent &&
               sourceFormat == OutputImageFormat.Jpeg
            ? OutputImageFormat.Png
            : sourceFormat;
    }

    private static string ToExtension(
        string sourcePath,
        OutputImageFormat format) =>
        format switch
        {
            OutputImageFormat.Jpeg => ".jpg",
            OutputImageFormat.Png => ".png",
            OutputImageFormat.Webp => ".webp",
            OutputImageFormat.Bmp => ".bmp",
            OutputImageFormat.Tiff => Path.GetExtension(sourcePath).Equals(
                ".tiff",
                StringComparison.OrdinalIgnoreCase)
                ? ".tiff"
                : ".tif",
            _ => Path.GetExtension(sourcePath)
        };

    private static void DeleteUnusedReservation(
        string outputPath,
        ProcessingRequest request)
    {
        if (request.Output.Mode == OutputMode.OverwriteOriginal ||
            !File.Exists(outputPath))
        {
            return;
        }

        if (new FileInfo(outputPath).Length == 0)
        {
            File.Delete(outputPath);
        }
    }
}
