using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Config;

public sealed record ConfigurationPackage(
    int SchemaVersion,
    string ProductName,
    DateTimeOffset ExportedAt,
    AppConfiguration Configuration,
    ProcessingPreset[] Presets)
{
    public const int CurrentSchemaVersion = 1;

    public const string CurrentProductName = "苏影枢";

    public static ConfigurationPackage Create(
        AppConfiguration configuration,
        IReadOnlyList<ProcessingPreset> presets,
        DateTimeOffset? exportedAt = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(presets);
        return new ConfigurationPackage(
            CurrentSchemaVersion,
            CurrentProductName,
            exportedAt ?? DateTimeOffset.Now,
            configuration,
            presets.ToArray());
    }
}
