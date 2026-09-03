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
