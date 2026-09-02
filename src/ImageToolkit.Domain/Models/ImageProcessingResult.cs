using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Models;

public sealed record ImageProcessingResult(
    string SourcePath,
    string? OutputPath,
    ImageProcessingStatus Status,
    long OutputSizeBytes,
    PixelSize? FinalSize,
    int? Quality,
    bool UsedAutomaticResize,
    bool UsedPngQuantization,
    string? ErrorCode,
    string? Message)
{
    public static ImageProcessingResult Completed(
        string sourcePath,
        string outputPath,
        long outputSizeBytes,
        PixelSize? finalSize = null,
        int? quality = null,
        bool usedAutomaticResize = false,
        bool usedPngQuantization = false) =>
        new(
            sourcePath,
            outputPath,
            ImageProcessingStatus.Completed,
            outputSizeBytes,
            finalSize,
            quality,
            usedAutomaticResize,
            usedPngQuantization,
            null,
            null);

    public static ImageProcessingResult Unmet(
        string sourcePath,
        string? outputPath,
        long outputSizeBytes,
        PixelSize? finalSize,
        string reason,
        int? quality = null) =>
        new(
            sourcePath,
            outputPath,
            ImageProcessingStatus.Unmet,
            outputSizeBytes,
            finalSize,
            quality,
            false,
            false,
            "compression.target-unmet",
            reason);

    public static ImageProcessingResult Failed(
        string sourcePath,
        string errorCode,
        string message) =>
        new(
            sourcePath,
            null,
            ImageProcessingStatus.Failed,
            0,
            null,
            null,
            false,
            false,
            errorCode,
            message);

    public static ImageProcessingResult Cancelled(string sourcePath) =>
        new(
            sourcePath,
            null,
            ImageProcessingStatus.Cancelled,
            0,
            null,
            null,
            false,
            false,
            "operation.cancelled",
            "处理已取消。");
}
