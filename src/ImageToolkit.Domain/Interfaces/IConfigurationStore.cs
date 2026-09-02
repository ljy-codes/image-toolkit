namespace ImageToolkit.Domain.Interfaces;

public interface IConfigurationStore<TConfiguration>
    where TConfiguration : class
{
    Task<TConfiguration> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(TConfiguration configuration, CancellationToken cancellationToken);
}
