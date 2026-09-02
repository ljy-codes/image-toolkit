namespace ImageToolkit.Domain.Interfaces;

public interface IBackgroundRemovalEngine
{
    Task RemoveBackgroundAsync(
        Stream input,
        Stream output,
        CancellationToken cancellationToken);
}
