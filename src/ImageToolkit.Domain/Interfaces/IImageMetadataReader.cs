using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IImageMetadataReader
{
    Task<ImageFileInfo> ReadAsync(string sourcePath, CancellationToken cancellationToken);
}
