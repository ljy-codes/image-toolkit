using System.Net;
using System.Security.Cryptography;
using ImageToolkit.Infrastructure.AI;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class LocalAiModelManagerTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Installs_verified_model_and_reports_progress()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = CreateManifest(bytes, Convert.ToHexString(SHA256.HashData(bytes)));
        var manager = CreateManager(bytes, manifest);
        var progressValues = new List<double>();
        var progress = new InlineProgress(value => progressValues.Add(value));

        await manager.InstallModelAsync(
            manifest.ModelId,
            progress,
            CancellationToken.None);

        var status = await manager.GetStatusAsync(
            manifest.ModelId,
            CancellationToken.None);
        Assert.True(status.IsInstalled);
        Assert.Equal(
            bytes,
            await File.ReadAllBytesAsync(
                await manager.GetModelPathAsync(
                    manifest.ModelId,
                    CancellationToken.None)));
        Assert.Contains(100, progressValues);
        Assert.All(
            progressValues,
            value => Assert.InRange(value, 0d, 100d));
        Assert.Empty(Directory.GetFiles(
            Path.Combine(_directory, ".downloads"),
            "*.tmp"));
    }

    [Fact]
    public async Task Rejects_sha256_mismatch_and_removes_temporary_file()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = CreateManifest(bytes, new string('0', 64));
        var manager = CreateManager(bytes, manifest);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.InstallModelAsync(
                manifest.ModelId,
                null,
                CancellationToken.None));

        Assert.Contains("SHA-256", exception.Message);
        Assert.Empty(Directory.GetFiles(
            Path.Combine(_directory, ".downloads"),
            "*.tmp"));
    }

    [Fact]
    public async Task Identifies_and_imports_verified_local_model_without_moving_source()
    {
        var bytes = new byte[] { 6, 7, 8, 9, 10 };
        var manifest = CreateManifest(
            bytes,
            Convert.ToHexString(SHA256.HashData(bytes)));
        var manager = CreateManager([], manifest);
        Directory.CreateDirectory(_directory);
        var sourcePath = Path.Combine(_directory, "incoming.onnx");
        await File.WriteAllBytesAsync(sourcePath, bytes);
        var progressValues = new List<double>();
        var progress = new InlineProgress(value => progressValues.Add(value));

        var candidate = await manager.IdentifyLocalModelAsync(
            sourcePath,
            progress,
            CancellationToken.None);
        await manager.ImportLocalModelAsync(
            candidate.ModelId,
            sourcePath,
            progress,
            CancellationToken.None);

        Assert.Equal(manifest.ModelId, candidate.ModelId);
        Assert.False(candidate.IsInstalled);
        Assert.True(File.Exists(sourcePath));
        Assert.Equal(
            bytes,
            await File.ReadAllBytesAsync(
                await manager.GetModelPathAsync(
                    manifest.ModelId,
                    CancellationToken.None)));
        Assert.Contains(100, progressValues);
        Assert.Empty(Directory.GetFiles(
            Path.Combine(_directory, ".imports"),
            "*.tmp"));
    }

    [Fact]
    public async Task Rejects_unknown_local_model_without_replacing_installed_model()
    {
        var installedBytes = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = CreateManifest(
            installedBytes,
            Convert.ToHexString(SHA256.HashData(installedBytes)));
        var manager = CreateManager(installedBytes, manifest);
        await manager.InstallModelAsync(
            manifest.ModelId,
            null,
            CancellationToken.None);
        Directory.CreateDirectory(_directory);
        var sourcePath = Path.Combine(_directory, "unknown.onnx");
        await File.WriteAllBytesAsync(sourcePath, new byte[] { 5, 4, 3, 2, 1 });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.IdentifyLocalModelAsync(
                sourcePath,
                null,
                CancellationToken.None));

        Assert.Contains("不是当前版本支持", exception.Message);
        Assert.Equal(
            installedBytes,
            await File.ReadAllBytesAsync(
                await manager.GetModelPathAsync(
                    manifest.ModelId,
                    CancellationToken.None)));
    }

    [Fact]
    public async Task Import_revalidates_local_model_and_preserves_installed_model()
    {
        var installedBytes = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = CreateManifest(
            installedBytes,
            Convert.ToHexString(SHA256.HashData(installedBytes)));
        var manager = CreateManager(installedBytes, manifest);
        await manager.InstallModelAsync(
            manifest.ModelId,
            null,
            CancellationToken.None);
        var sourcePath = Path.Combine(_directory, "changed.onnx");
        await File.WriteAllBytesAsync(sourcePath, new byte[] { 5, 4, 3, 2, 1 });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.ImportLocalModelAsync(
                manifest.ModelId,
                sourcePath,
                null,
                CancellationToken.None));

        Assert.Contains("不是当前版本支持", exception.Message);
        Assert.Equal(
            installedBytes,
            await File.ReadAllBytesAsync(
                await manager.GetModelPathAsync(
                    manifest.ModelId,
                    CancellationToken.None)));
        Assert.Empty(Directory.GetFiles(
            Path.Combine(_directory, ".imports"),
            "*.tmp"));
    }

    [Fact]
    public async Task Import_cancellation_preserves_existing_model_and_cleans_temporary_files()
    {
        var sourceBytes = new byte[2 * 1024 * 1024];
        RandomNumberGenerator.Fill(sourceBytes);
        var manifest = CreateManifest(
            sourceBytes,
            Convert.ToHexString(SHA256.HashData(sourceBytes)));
        var manager = CreateManager(sourceBytes, manifest);
        Directory.CreateDirectory(_directory);
        var targetPath = Path.Combine(_directory, manifest.FileName);
        var existingBytes = Enumerable.Repeat((byte)42, sourceBytes.Length).ToArray();
        await File.WriteAllBytesAsync(targetPath, existingBytes);
        var sourcePath = Path.Combine(_directory, "incoming.onnx");
        await File.WriteAllBytesAsync(sourcePath, sourceBytes);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress(value =>
        {
            if (value > 0)
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            manager.ImportLocalModelAsync(
                manifest.ModelId,
                sourcePath,
                progress,
                cancellation.Token));

        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(targetPath));
        Assert.Empty(Directory.GetFiles(
            Path.Combine(_directory, ".imports"),
            "*.tmp"));
    }

    [Fact]
    public async Task Sidecar_commit_failure_does_not_replace_existing_model()
    {
        var sourceBytes = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = CreateManifest(
            sourceBytes,
            Convert.ToHexString(SHA256.HashData(sourceBytes)));
        var manager = CreateManager(sourceBytes, manifest);
        Directory.CreateDirectory(_directory);
        var targetPath = Path.Combine(_directory, manifest.FileName);
        var existingBytes = new byte[] { 5, 4, 3, 2, 1 };
        await File.WriteAllBytesAsync(targetPath, existingBytes);
        Directory.CreateDirectory(targetPath + ".sha256");
        var sourcePath = Path.Combine(_directory, "incoming.onnx");
        await File.WriteAllBytesAsync(sourcePath, sourceBytes);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            manager.ImportLocalModelAsync(
                manifest.ModelId,
                sourcePath,
                null,
                CancellationToken.None));

        Assert.True(
            exception is IOException or UnauthorizedAccessException,
            $"Unexpected exception type: {exception.GetType().FullName}");
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(targetPath));
        Assert.Empty(Directory.GetFiles(
            Path.Combine(_directory, ".imports"),
            "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private LocalAiModelManager CreateManager(
        byte[] bytes,
        AiModelManifest manifest) =>
        new(
            new HttpClient(new StaticContentHandler(bytes)),
            _directory,
            [manifest]);

    private static AiModelManifest CreateManifest(byte[] bytes, string sha256) =>
        new(
            "test-model",
            "测试模型",
            new Uri("https://example.invalid/model.onnx"),
            "model.onnx",
            bytes.LongLength,
            sha256,
            "test",
            1,
            1);

    private sealed class StaticContentHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
