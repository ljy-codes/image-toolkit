using Microsoft.Extensions.Logging;
using ImageToolkit.Infrastructure.Logging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class RollingFileLoggerProviderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Writes_structured_exception_without_sensitive_value()
    {
        const string secret = "31.2304,121.4737";
        await using (var provider = new RollingFileLoggerProvider(
                         _directory,
                         sensitiveValues: [secret]))
        {
            var logger = provider.CreateLogger("ImageToolkit.Tests");
            logger.LogError(
                new EventId(42, "Decode"),
                new InvalidOperationException("decode failed"),
                "处理失败，位置 {Location}",
                secret);
        }

        var content = await File.ReadAllTextAsync(
            Assert.Single(Directory.EnumerateFiles(_directory, "*.log")));

        Assert.Contains("Error", content);
        Assert.Contains("42", content);
        Assert.Contains("处理失败", content);
        Assert.Contains(nameof(InvalidOperationException), content);
        Assert.DoesNotContain(secret, content);
    }

    [Fact]
    public async Task Keeps_only_newest_fourteen_log_files()
    {
        Directory.CreateDirectory(_directory);
        for (var day = 1; day <= 16; day++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(_directory, $"ImageToolkit-202608{day:00}.log"),
                "old");
        }

        await using (var provider = new RollingFileLoggerProvider(_directory))
        {
            provider.CreateLogger("test").LogInformation("current");
        }

        Assert.True(Directory.EnumerateFiles(_directory, "*.log").Count() <= 14);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
