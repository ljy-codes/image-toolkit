using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IFailedItemArchiver
{
    Task ArchiveAsync(
        ImageImportEntry entry,
        ImageProcessingResult result,
        CancellationToken cancellationToken);
}
