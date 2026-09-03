using System.Security.Cryptography;
using System.Text;
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
        var temporaryHashPath = temporaryPath + ".sha256.tmp";
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

            await WriteDurableTextFileAsync(
                temporaryHashPath,
                manifest.Sha256,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryHashPath, GetHashPath(targetPath), true);
            File.Move(temporaryPath, targetPath, true);
            progress?.Report(100);
        }
        finally
        {
            DeleteFileIfExists(temporaryPath);
            DeleteFileIfExists(temporaryHashPath);
        }
    }

    public async Task<AiModelStatus> IdentifyLocalModelAsync(
        string sourcePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("所选模型文件不存在。", fullSourcePath);
        }

        var sourceSize = new FileInfo(fullSourcePath).Length;
        var sizeMatches = _manifests.Values
            .Where(manifest => manifest.SizeBytes == sourceSize)
            .ToArray();
        if (sizeMatches.Length == 0)
        {
            throw CreateUnsupportedLocalModelException();
        }

        var sourceHash = await ComputeSha256Async(
            fullSourcePath,
            progress,
            cancellationToken).ConfigureAwait(false);
        var manifest = sizeMatches.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Sha256,
                sourceHash,
                StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
        {
            throw CreateUnsupportedLocalModelException();
        }

        return await GetStatusAsync(
            manifest.ModelId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ImportLocalModelAsync(
        string modelId,
        string sourcePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var manifest = GetManifest(modelId);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("所选模型文件不存在。", fullSourcePath);
        }

        Directory.CreateDirectory(_modelDirectory);
        var targetPath = GetModelPath(manifest);
        var importDirectory = Path.Combine(_modelDirectory, ".imports");
        Directory.CreateDirectory(importDirectory);
        if (string.Equals(
                fullSourcePath,
                targetPath,
                StringComparison.OrdinalIgnoreCase))
        {
            var existingModelHashTemporaryPath = Path.Combine(
                importDirectory,
                $"{manifest.FileName}.{Guid.NewGuid():N}.sha256.tmp");
            await ValidateModelFileAsync(
                fullSourcePath,
                manifest,
                progress,
                cancellationToken).ConfigureAwait(false);
            try
            {
                await WriteDurableTextFileAsync(
                    existingModelHashTemporaryPath,
                    manifest.Sha256,
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(
                    existingModelHashTemporaryPath,
                    GetHashPath(targetPath),
                    true);
            }
            finally
            {
                DeleteFileIfExists(existingModelHashTemporaryPath);
            }

            return;
        }

        var temporaryPath = Path.Combine(
            importDirectory,
            $"{manifest.FileName}.{Guid.NewGuid():N}.tmp");
        var temporaryHashPath = temporaryPath + ".sha256.tmp";

        try
        {
            var actualHash = await CopyAndComputeSha256Async(
                fullSourcePath,
                temporaryPath,
                progress,
                cancellationToken).ConfigureAwait(false);
            var actualSize = new FileInfo(temporaryPath).Length;
            if (actualSize != manifest.SizeBytes ||
                !string.Equals(
                    actualHash,
                    manifest.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw CreateUnsupportedLocalModelException();
            }

            await WriteDurableTextFileAsync(
                temporaryHashPath,
                manifest.Sha256,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryHashPath, GetHashPath(targetPath), true);
            File.Move(temporaryPath, targetPath, true);
            progress?.Report(100);
        }
        finally
        {
            DeleteFileIfExists(temporaryPath);
            DeleteFileIfExists(temporaryHashPath);
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

    private static InvalidDataException CreateUnsupportedLocalModelException() =>
        new("所选文件不是当前版本支持的 BiRefNet 人像或通用模型。");

    private static async Task ValidateModelFileAsync(
        string path,
        AiModelManifest manifest,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var actualSize = new FileInfo(path).Length;
        if (actualSize != manifest.SizeBytes)
        {
            throw CreateUnsupportedLocalModelException();
        }

        var actualHash = await ComputeSha256Async(
            path,
            progress,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
                actualHash,
                manifest.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw CreateUnsupportedLocalModelException();
        }
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
        => await ComputeSha256Async(
            path,
            null,
            cancellationToken).ConfigureAwait(false);

    private static async Task<string> ComputeSha256Async(
        string path,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        long processed = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            processed += read;
            progress?.Report(stream.Length == 0
                ? 100
                : processed * 100d / stream.Length);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task<string> CopyAndComputeSha256Async(
        string sourcePath,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
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

            await output.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
            copied += read;
            progress?.Report(input.Length == 0
                ? 100
                : copied * 100d / input.Length);
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        output.Flush(true);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task WriteDurableTextFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(content);
        await using var output = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        output.Flush(true);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
