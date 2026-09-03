using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Files;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class FailedItemArchiverTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Copies_folder_failure_and_writes_clear_reason_reports()
    {
        var root = Path.Combine(_directory, "图集");
        var child = Path.Combine(root, "子目录");
        Directory.CreateDirectory(child);
        var source = Path.Combine(child, "photo.png");
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        var entry = ImageImportEntry.FromFolder(root, source);
        var diagnostic = new ProcessingDiagnostic(
            "compression",
            "目标为 1.00 MB，最低可达 1.34 MB，本次未生成输出文件。",
            "target-unmet",
            1024 * 1024,
            1405092,
            ["启用 PNG 颜色量化。"]);
        var result = ImageProcessingResult.Unmet(
            source,
            1405092,
            new PixelSize(1000, 800),
            diagnostic.UserMessage,
            diagnostic);

        await new FailedItemArchiver().ArchiveAsync(
            entry,
            result,
            CancellationToken.None);

        var failedRoot = Path.Combine(_directory, "图集-未处理");
        var copied = Path.Combine(failedRoot, "子目录", "photo.png");
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(copied));
        Assert.Contains("最低可达 1.34 MB", await File.ReadAllTextAsync(
            Path.Combine(failedRoot, "失败原因.txt")));
        Assert.Contains("compression", await File.ReadAllTextAsync(
            Path.Combine(failedRoot, "失败原因.csv")));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(source));
    }

    [Fact]
    public async Task Single_file_failure_is_not_copied()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "photo.png");
        await File.WriteAllBytesAsync(source, [1]);
        var entry = ImageImportEntry.FromFile(source);
        var result = ImageProcessingResult.Failed(
            source,
            "read.failed",
            "无法读取图片。");

        await new FailedItemArchiver().ArchiveAsync(
            entry,
            result,
            CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(_directory, "photo-未处理")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
