using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Results;

namespace ImageToolkit.Application.Processing;

public sealed class ProcessingRequestValidator
{
    public ValidationResult Validate(ProcessingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();
        ValidateQuality(request, errors);
        ValidateResize(request, errors);
        ValidateAspectRatio(request, errors);
        ValidateCompressionLimits(request, errors);
        ValidateOutput(request, errors);
        return new ValidationResult(errors);
    }

    private static void ValidateQuality(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (request.Compression.MinimumJpegQuality is < 20 or > 95)
        {
            errors.Add(new(
                "compression.jpeg-quality-range",
                "JPEG 最低质量必须在 20 到 95 之间。"));
        }

        if (request.Compression.MinimumWebpQuality is < 20 or > 95)
        {
            errors.Add(new(
                "compression.webp-quality-range",
                "WebP 最低质量必须在 20 到 95 之间。"));
        }
    }

    private static void ValidateResize(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (!request.Resize.Enabled)
        {
            return;
        }

        if (request.Resize.Width is <= 0)
        {
            errors.Add(new("resize.width-positive", "宽度必须大于 0。"));
        }

        if (request.Resize.Height is <= 0)
        {
            errors.Add(new("resize.height-positive", "高度必须大于 0。"));
        }

        if (request.Resize.Width is null && request.Resize.Height is null)
        {
            errors.Add(new("resize.dimension-required", "至少需要填写宽度或高度。"));
        }

        if (!request.Resize.LockAspectRatio &&
            (request.Resize.Width is null || request.Resize.Height is null))
        {
            errors.Add(new(
                "resize.stretch-dimensions-required",
                "不锁定比例时必须同时填写宽度和高度。"));
        }
    }

    private static void ValidateAspectRatio(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (request.AspectRatio.Mode == AspectRatioMode.Original)
        {
            return;
        }

        if (request.AspectRatio.RatioWidth <= 0)
        {
            errors.Add(new("aspect-ratio.width-positive", "目标比例宽度必须大于 0。"));
        }

        if (request.AspectRatio.RatioHeight <= 0)
        {
            errors.Add(new("aspect-ratio.height-positive", "目标比例高度必须大于 0。"));
        }
    }

    private static void ValidateCompressionLimits(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (!request.Compression.Enabled)
        {
            return;
        }

        if (request.Compression.TargetBytes <= 0)
        {
            errors.Add(new("compression.target-positive", "目标文件大小必须大于 0。"));
        }

        if (request.Compression.MinimumScaleRatio is <= 0 or > 1)
        {
            errors.Add(new(
                "compression.scale-range",
                "自动缩放比例下限必须大于 0 且不超过 1。"));
        }

        if (request.Compression.MinimumShortEdge <= 0)
        {
            errors.Add(new(
                "compression.short-edge-positive",
                "最小短边必须大于 0。"));
        }
    }

    private static void ValidateOutput(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(request.Output.FileNameSuffix))
        {
            errors.Add(new("output.suffix-required", "输出文件名后缀不能为空。"));
        }

        if (request.Output.Mode == OutputMode.SpecificDirectory &&
            string.IsNullOrWhiteSpace(request.Output.DirectoryPath))
        {
            errors.Add(new("output.directory-required", "请选择输出目录。"));
        }

        if (request.Output.Mode == OutputMode.OverwriteOriginal &&
            request.Output.Format != OutputImageFormat.Original)
        {
            errors.Add(new(
                "output.overwrite-format",
                "覆盖原文件时必须保持原格式。"));
        }

        if (request.Output.Mode == OutputMode.OverwriteOriginal &&
            request.Background.Mode == BackgroundMode.Transparent)
        {
            errors.Add(new(
                "output.overwrite-transparent",
                "透明背景不能覆盖可能不支持透明通道的原格式。"));
        }
    }
}
