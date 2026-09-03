using System.Security.Cryptography;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.AI;

public sealed class LocalAiModelManager : IAiModelManager
{
    private readonly HttpClient _httpClient;
    private readonly string _modelDirectory;
    private readonly IReadOnlyDictionary<string, AiModelManifest> _manifests;

    public LocalAiModelManager(
        HttpClient httpClient,
        string modelDirectory,
        IEnumerable<AiModelManifest>? manifests = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);
        _modelDirectory = Path.GetFullPath(modelDirectory);
        _manifests = (manifests ?? AiModelManifest.Defaults).ToDictionary(
            manifest => manifest.ModelId,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<AiModelStatus> GetStatusAsync(
        string modelId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifest = GetManifest(modelId);
        var path = GetModelPath(manifest);
        var installed = File.Exists(path) &&
                        new FileInfo(path).Length == manifest.SizeBytes &&
                        File.Exists(GetHashPath(path)) &&
                        string.Equals(
                            File.ReadAllText(GetHashPath(path)).Trim(),
                            manifest.Sha256,
                            StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new AiModelStatus(
            manifest.ModelId,
            manifest.DisplayName,
            manifest.SizeBytes,
            installed,
            manifest.License));
    }

    public async Task InstallModelAsync(
        string modelId,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var manifest = GetManifest(modelId);
        Directory.CreateDirectory(_modelDirectory);
        var downloadDirectory = Path.Combine(_modelDirectory, ".downloads");
        Directory.CreateDirectory(downloadDirectory);
        var temporaryPath = Path.Combine(
            downloadDirectory,
            $"{manifest.FileName}.{Guid.NewGuid():N}.tmp");
        var targetPath = GetModelPath(manifest);

        try
        {
            using var response = await _httpClient.GetAsync(
                manifest.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? manifest.SizeBytes;
            await using var input = await response.Content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 1024];
                long copied = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                    copied += read;
                    progress?.Report(total <= 0 ? 0 : copied * 100d / total);
                }

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(true);
            }

            var actualSize = new FileInfo(temporaryPath).Length;
            if (actualSize != manifest.SizeBytes)
            {
                throw new InvalidDataException(
                    $"模型文件大小校验失败：预期 {manifest.SizeBytes} 字节，实际 {actualSize} 字节。");
            }

            var actualHash = await ComputeSha256Async(
                temporaryPath,
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(
                    actualHash,
                    manifest.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"模型 SHA-256 校验失败：预期 {manifest.Sha256}，实际 {actualHash}。");
            }

            File.Move(temporaryPath, targetPath, true);
            await File.WriteAllTextAsync(
                GetHashPath(targetPath),
                manifest.Sha256,
                cancellationToken).ConfigureAwait(false);
            progress?.Report(100);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task RemoveModelAsync(
        string modelId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetModelPath(GetManifest(modelId));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var hashPath = GetHashPath(path);
        if (File.Exists(hashPath))
        {
            File.Delete(hashPath);
        }

        return Task.CompletedTask;
    }

    public async Task<string> GetModelPathAsync(
        string modelId,
        CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(modelId, cancellationToken)
            .ConfigureAwait(false);
        if (!status.IsInstalled)
        {
            throw new FileNotFoundException(
                $"尚未安装“{status.DisplayName}”，请先在 AI 抠图设置中安装模型。");
        }

        return GetModelPath(GetManifest(modelId));
    }

    private AiModelManifest GetManifest(string modelId) =>
        _manifests.TryGetValue(modelId, out var manifest)
            ? manifest
            : throw new ArgumentOutOfRangeException(
                nameof(modelId),
                $"未知 AI 模型：{modelId}");

    private string GetModelPath(AiModelManifest manifest) =>
        Path.Combine(_modelDirectory, manifest.FileName);

    private static string GetHashPath(string modelPath) => modelPath + ".sha256";

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
