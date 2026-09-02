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
        CancellationToken cancellationToken) =>
        Task.Run(
            () => Render(
                sourcePath,
                request,
                maximumWidth,
                maximumHeight,
                cancellationToken),
            cancellationToken);

    private PreviewImage Render(
        string sourcePath,
        ProcessingRequest request,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();
        _metadata.ApplyInputOrientation(image);
        MagickImageProcessor.ApplyAspectRatio(
            image,
            request.AspectRatio,
            request.Background);
        MagickImageProcessor.ApplyResize(image, request.Resize);
        cancellationToken.ThrowIfCancellationRequested();

        var previewGeometry = new MagickGeometry(
            (uint)maximumWidth,
            (uint)maximumHeight)
        {
            IgnoreAspectRatio = false,
            Greater = true
        };
        image.Resize(previewGeometry);
        cancellationToken.ThrowIfCancellationRequested();
        image.Format = MagickFormat.Png;
        using var stream = new MemoryStream();
        image.Write(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return new PreviewImage(
            stream.ToArray(),
            new PixelSize((int)image.Width, (int)image.Height));
    }
}
