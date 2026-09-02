using ImageToolkit.Infrastructure.Files;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class ImageFileDiscoveryTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Discovers_supported_files_and_reports_unsupported_files()
    {
        Directory.CreateDirectory(_directory);
        var image = Path.Combine(_directory, "one.jpg");
        var unsupported = Path.Combine(_directory, "notes.txt");
        await File.WriteAllBytesAsync(image, [1]);
        await File.WriteAllTextAsync(unsupported, "text");
        var discovery = new ImageFileDiscovery();

        var result = await discovery.DiscoverAsync(
            [_directory],
            false,
            CancellationToken.None);

        Assert.Equal([Path.GetFullPath(image)], result.Files);
        Assert.Contains(
            result.Rejected,
            item => item.Path == Path.GetFullPath(unsupported));
    }

    [Fact]
    public async Task Includes_subdirectories_only_when_requested()
    {
        var child = Path.Combine(_directory, "child");
        Directory.CreateDirectory(child);
        var rootImage = Path.Combine(_directory, "root.png");
        var childImage = Path.Combine(child, "child.webp");
        await File.WriteAllBytesAsync(rootImage, [1]);
        await File.WriteAllBytesAsync(childImage, [1]);
        var discovery = new ImageFileDiscovery();

        var shallow = await discovery.DiscoverAsync(
            [_directory],
            false,
            CancellationToken.None);
        var recursive = await discovery.DiscoverAsync(
            [_directory],
            true,
            CancellationToken.None);

        Assert.Equal([Path.GetFullPath(rootImage)], shallow.Files);
        Assert.Equal(2, recursive.Files.Count);
        Assert.Contains(Path.GetFullPath(childImage), recursive.Files);
    }

    [Fact]
    public async Task Collapses_duplicate_paths_case_insensitively()
    {
        Directory.CreateDirectory(_directory);
        var image = Path.Combine(_directory, "Photo.JPG");
        await File.WriteAllBytesAsync(image, [1]);
        var discovery = new ImageFileDiscovery();

        var result = await discovery.DiscoverAsync(
            [image, image.ToUpperInvariant()],
            false,
            CancellationToken.None);

        Assert.Single(result.Files);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
