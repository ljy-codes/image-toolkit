using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IImageProcessor
{
    Task<ImageProcessingResult> ProcessAsync(
        string sourcePath,
        string outputPath,
        ProcessingRequest request,
        CancellationToken cancellationToken);
}
