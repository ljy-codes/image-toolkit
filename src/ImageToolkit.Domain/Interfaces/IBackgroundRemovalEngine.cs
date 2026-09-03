using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Interfaces;

public interface IBackgroundRemovalEngine
{
    Task RemoveBackgroundAsync(
        Stream input,
        Stream output,
        BackgroundRemovalMode mode,
        CancellationToken cancellationToken);
}
