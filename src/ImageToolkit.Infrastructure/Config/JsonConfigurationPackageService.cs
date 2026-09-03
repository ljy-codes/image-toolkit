using System.Text.Json;
using System.Text.Json.Serialization;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Config;

public interface IConfigurationPackageService
{
    Task ExportAsync(
        string path,
        ConfigurationPackage package,
        CancellationToken cancellationToken);

    Task<ConfigurationPackage> ImportAsync(
        string path,
        CancellationToken cancellationToken);
}

public sealed class JsonConfigurationPackageService : IConfigurationPackageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task ExportAsync(
        string path,
        ConfigurationPackage package,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(package);
        Validate(package);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("配置包路径缺少目录。", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
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
                    package,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, backupPath, true);
                File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            DeleteIfExists(temporaryPath);
            DeleteIfExists(backupPath);
        }
    }

    public async Task<ConfigurationPackage> ImportAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using var stream = new FileStream(
                Path.GetFullPath(path),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65536,
                FileOptions.Asynchronous);
            var package = await JsonSerializer.DeserializeAsync<ConfigurationPackage>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (package is null)
            {
                throw new InvalidDataException("配置包内容为空。");
            }

            Validate(package);
            return package;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException or IOException)
        {
            throw new InvalidDataException(
                $"无法读取配置包：{exception.Message}",
                exception);
        }
    }

    private static void Validate(ConfigurationPackage package)
    {
        if (!string.Equals(
                package.ProductName,
                ConfigurationPackage.CurrentProductName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("该文件不是苏影枢配置包。");
        }

        if (package.SchemaVersion != ConfigurationPackage.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"不支持配置包版本 {package.SchemaVersion}，当前支持版本为 {ConfigurationPackage.CurrentSchemaVersion}。");
        }

        if (package.Configuration is null)
        {
            throw new InvalidDataException("配置包缺少应用配置。");
        }

        if (package.Presets is null)
        {
            throw new InvalidDataException("配置包缺少命名预设集合。");
        }

        ValidateRequest(package.Configuration.Processing, "当前配置");
        foreach (var preset in package.Presets)
        {
            if (preset is null)
            {
                throw new InvalidDataException("配置包的命名预设中存在空项目。");
            }

            ValidateRequest(preset.Request, $"命名预设“{preset.Name}”");
        }

        if (package.ExportedAt == default)
        {
            throw new InvalidDataException("配置包缺少导出时间。");
        }
    }

    private static void ValidateRequest(
        ProcessingRequest? request,
        string context)
    {
        if (request is null)
        {
            throw new InvalidDataException($"配置包{context}缺少处理参数。");
        }

        if (request.Compression is null ||
            request.Resize is null ||
            request.AspectRatio is null ||
            request.AiBackgroundRemoval is null ||
            request.Background is null ||
            request.Metadata is null ||
            request.Output is null)
        {
            throw new InvalidDataException(
                $"配置包{context}的处理参数结构不完整。");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
