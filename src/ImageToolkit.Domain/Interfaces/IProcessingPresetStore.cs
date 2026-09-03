using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IProcessingPresetStore
{
    Task<IReadOnlyList<ProcessingPreset>> LoadAsync(
        CancellationToken cancellationToken);

    Task SaveAsync(
        IReadOnlyList<ProcessingPreset> presets,
        CancellationToken cancellationToken);
}
