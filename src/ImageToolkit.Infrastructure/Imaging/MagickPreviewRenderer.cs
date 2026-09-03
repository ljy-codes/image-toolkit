using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class MagickPreviewRenderer : IImagePreviewRenderer
{
    private readonly IBackgroundRemovalEngine? _backgroundRemovalEngine;
    private readonly MagickMetadataProcessor _metadata = new();

    public MagickPreviewRenderer(
        IBackgroundRemovalEngine? backgroundRemovalEngine = null)
    {
        _backgroundRemovalEngine = backgroundRemovalEngine;
    }

    public async Task<PreviewImage> RenderAsync(
        string sourcePath,
        ProcessingRequest request,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var sourceImage = new MagickImage(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();
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
        await _backgroundRemovalEngine.RemoveBackgroundAsync(
                input,
                output,
                mode,
                cancellationToken)
            .ConfigureAwait(false);
        output.Position = 0;
        return new MagickImage(output);
    }
}
