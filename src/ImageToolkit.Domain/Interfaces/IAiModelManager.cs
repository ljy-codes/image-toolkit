using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IAiModelManager
{
    Task<AiModelStatus> GetStatusAsync(
        string modelId,
        CancellationToken cancellationToken);

    Task InstallModelAsync(
        string modelId,
        IProgress<double>? progress,
        CancellationToken cancellationToken);

    Task<AiModelStatus> IdentifyLocalModelAsync(
        string sourcePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken);

    Task ImportLocalModelAsync(
        string modelId,
        string sourcePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken);

    Task RemoveModelAsync(string modelId, CancellationToken cancellationToken);

    Task<string> GetModelPathAsync(
        string modelId,
        CancellationToken cancellationToken);
}
