namespace ImageToolkit.Domain.Interfaces;

public interface IAiModelManager
{
    Task<bool> IsModelAvailableAsync(string modelId, CancellationToken cancellationToken);

    Task InstallModelAsync(string modelId, CancellationToken cancellationToken);

    Task RemoveModelAsync(string modelId, CancellationToken cancellationToken);
}
