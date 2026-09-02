using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Models;

public sealed record BatchItem(
    string SourcePath,
    BatchItemStatus Status,
    ImageProcessingResult? Result)
{
    public static BatchItem Waiting(string sourcePath) =>
        new(sourcePath, BatchItemStatus.Waiting, null);
}
