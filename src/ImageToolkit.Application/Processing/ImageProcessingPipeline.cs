using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Processing;

public sealed class ImageProcessingPipeline
{
    private readonly ProcessingRequestValidator _validator;
    private readonly IOutputPathResolver _pathResolver;
    private readonly IImageProcessor _processor;
    private readonly IFailedItemArchiver? _failedItemArchiver;

    public ImageProcessingPipeline(
        ProcessingRequestValidator validator,
        IOutputPathResolver pathResolver,
        IImageProcessor processor,
        IFailedItemArchiver? failedItemArchiver = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _failedItemArchiver = failedItemArchiver;
    }

    public Task<ImageProcessingResult> ProcessAsync(
        string sourcePath,
        ProcessingRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            ImageImportEntry.FromFile(sourcePath),
            request,
            cancellationToken);

    public async Task<ImageProcessingResult> ProcessAsync(
        ImageImportEntry entry,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var validation = _validator.Validate(request);
        if (!validation.IsValid)
        {
            var invalidResult = ImageProcessingResult.Failed(
                entry.SourcePath,
                validation.Errors[0].Code,
                validation.Errors[0].Message);
            return await ArchiveIfNeededAsync(entry, invalidResult)
                .ConfigureAwait(false);
        }

        var effectiveFormat = ResolveFormat(entry.SourcePath, request);
        var effectiveRequest = request with
        {
            Output = request.Output with { Format = effectiveFormat }
        };
        var extension = ToExtension(entry.SourcePath, effectiveFormat);
        string? outputPath = null;

        try
        {
            outputPath = _pathResolver.Resolve(
                entry,
                effectiveRequest.Output,
                extension);
            var result = await _processor.ProcessAsync(
                entry.SourcePath,
                outputPath,
                effectiveRequest,
                cancellationToken).ConfigureAwait(false);
            return await ArchiveIfNeededAsync(entry, result).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DeleteUnusedReservation(outputPath, effectiveRequest);
            return ImageProcessingResult.Cancelled(entry.SourcePath);
        }
        catch (ImageProcessingStageException exception)
        {
            DeleteUnusedReservation(outputPath, effectiveRequest);
            var failedResult = ImageProcessingResult.Failed(
                entry.SourcePath,
                exception.ErrorCode,
                exception.Diagnostic.UserMessage,
                exception.Diagnostic);
            return await ArchiveIfNeededAsync(entry, failedResult)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            DeleteUnusedReservation(outputPath, effectiveRequest);
            var message =
                $"处理未完成：{exception.Message} 本次未生成输出文件，原图保持不变。";
            var failedResult = ImageProcessingResult.Failed(
                entry.SourcePath,
                "processing.failed",
                message,
                new ProcessingDiagnostic(
                    "处理",
                    message,
                    exception.ToString(),
                    null,
                    null,
                    ["检查源文件是否可读取。", "检查输出目录权限或更换保存位置后重试。"]));
            return await ArchiveIfNeededAsync(entry, failedResult)
                .ConfigureAwait(false);
        }
    }

    private async Task<ImageProcessingResult> ArchiveIfNeededAsync(
        ImageImportEntry entry,
        ImageProcessingResult result)
    {
        if (_failedItemArchiver is null ||
            result.Status is not (ImageProcessingStatus.Unmet or ImageProcessingStatus.Failed))
        {
            return result;
        }

        try
        {
            await _failedItemArchiver.ArchiveAsync(
                entry,
                result,
                CancellationToken.None).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception)
        {
            var diagnostic = result.Diagnostic ?? new ProcessingDiagnostic(
                "处理",
                result.Message ?? "处理失败。",
                null,
                null,
                null,
                []);
            return result with
            {
                Message =
                    $"{result.Message ?? diagnostic.UserMessage} " +
                    $"失败文件归档未完成：{exception.Message}",
                Diagnostic = diagnostic with
                {
                    TechnicalMessage =
                        $"{diagnostic.TechnicalMessage ?? result.ErrorCode}；" +
                        $"archive.failed: {exception}"
                }
            };
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
        string? outputPath,
        ProcessingRequest request)
    {
        if (request.Output.Mode == OutputMode.OverwriteOriginal ||
            string.IsNullOrWhiteSpace(outputPath) ||
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
