using System.Text.Json;
using System.Text.Json.Serialization;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Config;

public sealed class JsonProcessingPresetStore : IProcessingPresetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _path;

    public JsonProcessingPresetStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public async Task<IReadOnlyList<ProcessingPreset>> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            await using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65536,
                FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<ProcessingPreset[]>(
                       stream,
                       SerializerOptions,
                       cancellationToken).ConfigureAwait(false)
                   ?? [];
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            PreserveCorruptFile();
            return [];
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<ProcessingPreset> presets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presets);
        var directory = Path.GetDirectoryName(_path)
            ?? throw new ArgumentException("预设路径缺少目录。", nameof(_path));
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + $".{Guid.NewGuid():N}.tmp";
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
                    presets,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, backupPath, true);
                File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
        finally
        {
            DeleteIfExists(temporaryPath);
            DeleteIfExists(backupPath);
        }
    }

    private void PreserveCorruptFile()
    {
        var directory = Path.GetDirectoryName(_path)!;
        var name = Path.GetFileNameWithoutExtension(_path);
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmssfff");
        var corruptPath = Path.Combine(directory, $"{name}.{timestamp}.corrupt");
        File.Move(_path, corruptPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
