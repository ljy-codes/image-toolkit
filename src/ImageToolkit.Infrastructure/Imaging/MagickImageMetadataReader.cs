using ImageMagick;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class MagickImageMetadataReader : IImageMetadataReader
{
    public Task<ImageFileInfo> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(sourcePath);
        image.AutoOrient();
        var file = new FileInfo(sourcePath);
        return Task.FromResult(new ImageFileInfo(
            file.FullName,
            file.Name,
            file.Extension,
            file.Length,
            new PixelSize((int)image.Width, (int)image.Height),
            image.HasAlpha));
    }
}
