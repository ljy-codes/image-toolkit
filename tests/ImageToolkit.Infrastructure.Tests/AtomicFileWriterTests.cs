using System.Text;
using ImageToolkit.Infrastructure.Files;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Failed_validation_preserves_original_file()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "photo.jpg");
        await File.WriteAllTextAsync(target, "original");
        var writer = new AtomicFileWriter();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            writer.ReplaceAsync(
                target,
                stream => stream.WriteAsync("new"u8.ToArray()).AsTask(),
                _ => Task.FromResult(false),
                CancellationToken.None));

        Assert.Equal("original", await File.ReadAllTextAsync(target));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task Successful_replacement_changes_target()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "photo.jpg");
        await File.WriteAllTextAsync(target, "original");
        var writer = new AtomicFileWriter();

        await writer.ReplaceAsync(
            target,
            stream => stream.WriteAsync("replacement"u8.ToArray()).AsTask(),
            path => Task.FromResult(new FileInfo(path).Length > 0),
            CancellationToken.None);

        Assert.Equal("replacement", await File.ReadAllTextAsync(target));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task Writes_reserved_new_output()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "photo-已处理.jpg");
        await using (File.Create(target))
        {
        }

        var writer = new AtomicFileWriter();
        await writer.WriteNewAsync(
            target,
            stream => stream.WriteAsync(Encoding.UTF8.GetBytes("new file")).AsTask(),
            path => Task.FromResult(new FileInfo(path).Length > 0),
            CancellationToken.None);

        Assert.Equal("new file", await File.ReadAllTextAsync(target));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task Cancellation_preserves_original_file()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "photo.jpg");
        await File.WriteAllTextAsync(target, "original");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var writer = new AtomicFileWriter();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.ReplaceAsync(
                target,
                _ => Task.CompletedTask,
                _ => Task.FromResult(true),
                cancellation.Token));

        Assert.Equal("original", await File.ReadAllTextAsync(target));
        AssertNoTemporaryFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private void AssertNoTemporaryFiles()
    {
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.bak"));
    }
}
