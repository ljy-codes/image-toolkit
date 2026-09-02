using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageToolkit.Domain.Interfaces;

namespace ImageToolkit.Infrastructure.Config;

public sealed class JsonConfigurationStore : IConfigurationStore<AppConfiguration>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _configurationPath;

    public JsonConfigurationStore(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        _configurationPath = Path.GetFullPath(configurationPath);
    }

    public async Task<AppConfiguration> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_configurationPath))
        {
            return AppConfiguration.Default;
        }

        try
        {
            await using var stream = new FileStream(
                _configurationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65536,
                FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<AppConfiguration>(
                       stream,
                       SerializerOptions,
                       cancellationToken).ConfigureAwait(false)
                   ?? AppConfiguration.Default;
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            PreserveCorruptConfiguration();
            return AppConfiguration.Default;
        }
    }

    public async Task SaveAsync(
        AppConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directory = Path.GetDirectoryName(_configurationPath)
            ?? throw new ArgumentException("配置路径缺少目录。", nameof(_configurationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = _configurationPath + $".{Guid.NewGuid():N}.tmp";
        var backupPath = temporaryPath + ".bak";

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             65536,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    configuration,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (File.Exists(_configurationPath))
            {
                File.Replace(temporaryPath, _configurationPath, backupPath, true);
                File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, _configurationPath);
            }
        }
        finally
        {
            DeleteIfExists(temporaryPath);
            DeleteIfExists(backupPath);
        }
    }

    private void PreserveCorruptConfiguration()
    {
        var directory = Path.GetDirectoryName(_configurationPath)!;
        var fileName = Path.GetFileNameWithoutExtension(_configurationPath);
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmssfff");
        var corruptPath = Path.Combine(directory, $"{fileName}.{timestamp}.corrupt");
        if (File.Exists(corruptPath))
        {
            corruptPath = Path.Combine(
                directory,
                $"{fileName}.{timestamp}.{Guid.NewGuid():N}.corrupt");
        }

        File.Move(_configurationPath, corruptPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
