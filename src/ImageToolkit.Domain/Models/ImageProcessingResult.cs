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
    string? Message,
    ProcessingDiagnostic? Diagnostic = null)
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
        long outputSizeBytes,
        PixelSize? finalSize,
        string reason,
        ProcessingDiagnostic diagnostic,
        int? quality = null,
        bool usedAutomaticResize = false,
        bool usedPngQuantization = false) =>
        new(
            sourcePath,
            null,
            ImageProcessingStatus.Unmet,
            outputSizeBytes,
            finalSize,
            quality,
            usedAutomaticResize,
            usedPngQuantization,
            "compression.target-unmet",
            reason,
            diagnostic);

    public static ImageProcessingResult Failed(
        string sourcePath,
        string errorCode,
        string message) =>
        Failed(
            sourcePath,
            errorCode,
            message,
            new ProcessingDiagnostic(
                "处理",
                message,
                null,
                null,
                null,
                []));

    public static ImageProcessingResult Failed(
        string sourcePath,
        string errorCode,
        string message,
        ProcessingDiagnostic diagnostic) =>
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
            message,
            diagnostic);

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
            "处理已取消。",
            new ProcessingDiagnostic(
                "operation",
                "处理已取消。",
                null,
                null,
                null,
                []));
}
