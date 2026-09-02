using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Infrastructure.Imaging;

public static class MagickGeometryCalculator
{
    public static PixelRectangle CalculateCrop(
        PixelSize source,
        int ratioWidth,
        int ratioHeight,
        CropAnchor anchor)
    {
        ValidateGeometry(source, ratioWidth, ratioHeight);

        var targetRatio = (double)ratioWidth / ratioHeight;
        var sourceRatio = (double)source.Width / source.Height;
        var width = source.Width;
        var height = source.Height;

        if (sourceRatio > targetRatio)
        {
            width = (int)Math.Round(source.Height * targetRatio);
        }
        else if (sourceRatio < targetRatio)
        {
            height = (int)Math.Round(source.Width / targetRatio);
        }

        return PositionCrop(source, new PixelSize(width, height), anchor);
    }

    public static PixelSize CalculateCanvas(
        PixelSize source,
        int ratioWidth,
        int ratioHeight)
    {
        ValidateGeometry(source, ratioWidth, ratioHeight);

        var targetRatio = (double)ratioWidth / ratioHeight;
        var sourceRatio = (double)source.Width / source.Height;

        if (Math.Abs(sourceRatio - targetRatio) < double.Epsilon)
        {
            return source;
        }

        return sourceRatio > targetRatio
            ? new PixelSize(source.Width, (int)Math.Ceiling(source.Width / targetRatio))
            : new PixelSize((int)Math.Ceiling(source.Height * targetRatio), source.Height);
    }

    public static PixelSize CalculateResize(PixelSize source, ResizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(source), "原图尺寸必须大于 0。");
        }

        if (!options.Enabled)
        {
            return source;
        }

        if (options.Width is null && options.Height is null)
        {
            throw new ArgumentException("至少需要指定宽度或高度。", nameof(options));
        }

        PixelSize target;
        if (options.Width is int width && options.Height is int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "目标尺寸必须大于 0。");
            }

            if (options.LockAspectRatio)
            {
                var expectedHeight = (int)Math.Round((double)width * source.Height / source.Width);
                if (expectedHeight != height)
                {
                    throw new ArgumentException("目标宽高与原图比例不兼容。", nameof(options));
                }
            }

            target = new PixelSize(width, height);
        }
        else if (options.Width is int widthOnly)
        {
            if (widthOnly <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "目标宽度必须大于 0。");
            }

            target = new PixelSize(
                widthOnly,
                (int)Math.Round((double)widthOnly * source.Height / source.Width));
        }
        else
        {
            var heightOnly = options.Height!.Value;
            if (heightOnly <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "目标高度必须大于 0。");
            }

            target = new PixelSize(
                (int)Math.Round((double)heightOnly * source.Width / source.Height),
                heightOnly);
        }

        if (options.AllowUpscale)
        {
            return target;
        }

        if (options.LockAspectRatio &&
            (target.Width > source.Width || target.Height > source.Height))
        {
            return source;
        }

        return new PixelSize(
            Math.Min(target.Width, source.Width),
            Math.Min(target.Height, source.Height));
    }

    private static PixelRectangle PositionCrop(
        PixelSize source,
        PixelSize crop,
        CropAnchor anchor)
    {
        var centeredX = (source.Width - crop.Width) / 2;
        var centeredY = (source.Height - crop.Height) / 2;

        return anchor switch
        {
            CropAnchor.Top => new(centeredX, 0, crop.Width, crop.Height),
            CropAnchor.Bottom => new(
                centeredX,
                source.Height - crop.Height,
                crop.Width,
                crop.Height),
            CropAnchor.Left => new(0, centeredY, crop.Width, crop.Height),
            CropAnchor.Right => new(
                source.Width - crop.Width,
                centeredY,
                crop.Width,
                crop.Height),
            _ => new(centeredX, centeredY, crop.Width, crop.Height)
        };
    }

    private static void ValidateGeometry(
        PixelSize source,
        int ratioWidth,
        int ratioHeight)
    {
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(source), "原图尺寸必须大于 0。");
        }

        if (ratioWidth <= 0 || ratioHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratioWidth), "目标比例必须大于 0。");
        }
    }
}
