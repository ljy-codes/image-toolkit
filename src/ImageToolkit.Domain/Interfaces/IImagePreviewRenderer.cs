using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IImagePreviewRenderer
{
    Task<PreviewImage> RenderAsync(
        string sourcePath,
        ProcessingRequest request,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken);
}
