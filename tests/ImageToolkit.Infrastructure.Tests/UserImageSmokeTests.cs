using System.Security.Cryptography;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Files;
using ImageToolkit.Infrastructure.Imaging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class UserImageSmokeTests : IDisposable
{
    private readonly string _temporaryDirectory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitUserAssets", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "UserAssets")]
    public async Task Processes_copies_without_modifying_user_images()
    {
        var sourceDirectory = FindUserImageDirectory();
        if (sourceDirectory is null)
        {
            return;
        }

        var sourceFiles = Directory
            .EnumerateFiles(sourceDirectory)
            .Where(path => IsSupported(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(sourceFiles);

        Directory.CreateDirectory(_temporaryDirectory);
        var pipeline = new ImageProcessingPipeline(
            new ProcessingRequestValidator(),
            new OutputPathResolver(),
            new MagickImageProcessor(new AtomicFileWriter()));

        foreach (var source in sourceFiles)
        {
            var sourceHash = ComputeHash(source);
            var copy = Path.Combine(_temporaryDirectory, Path.GetFileName(source));
            File.Copy(source, copy);

            var result = await pipeline.ProcessAsync(
                copy,
                ProcessingRequest.Default,
                CancellationToken.None);

            Assert.Contains(
                result.Status,
                new[]
                {
                    ImageProcessingStatus.Completed,
                    ImageProcessingStatus.Unmet
                });
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.Equal(sourceHash, ComputeHash(source));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, true);
        }
    }

    private static string? FindUserImageDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "测试图");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsSupported(string extension) =>
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
}
