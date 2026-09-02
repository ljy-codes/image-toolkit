using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class MagickPreviewRenderer : IImagePreviewRenderer
{
    private readonly MagickMetadataProcessor _metadata = new();

    public Task<PreviewImage> RenderAsync(
        string sourcePath,
        ProcessingRequest request,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(sourcePath);
        _metadata.ApplyInputOrientation(image);
        MagickImageProcessor.ApplyAspectRatio(
            image,
            request.AspectRatio,
            request.Background);
        MagickImageProcessor.ApplyResize(image, request.Resize);

        var previewGeometry = new MagickGeometry(
            (uint)maximumWidth,
            (uint)maximumHeight)
        {
            IgnoreAspectRatio = false,
            Greater = true
        };
        image.Resize(previewGeometry);
        image.Format = MagickFormat.Png;
        using var stream = new MemoryStream();
        image.Write(stream);
        var result = new PreviewImage(
            stream.ToArray(),
            new PixelSize((int)image.Width, (int)image.Height));
        return Task.FromResult(result);
    }
}
