using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Files;

public sealed class ImageFileDiscovery : IImageFileDiscovery
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".bmp",
            ".tif",
            ".tiff"
        };

    public Task<ImageImportResult> DiscoverAsync(
        IEnumerable<string> inputPaths,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rejected = new List<RejectedPath>();

        foreach (var rawPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                AddFile(path, files, rejected);
            }
            else if (Directory.Exists(path))
            {
                DiscoverDirectory(
                    path,
                    includeSubdirectories,
                    files,
                    rejected,
                    cancellationToken);
            }
            else
            {
                rejected.Add(new RejectedPath(path, "路径不存在。"));
            }
        }

        return Task.FromResult(new ImageImportResult(
            files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            rejected));
    }

    private static void DiscoverDirectory(
        string root,
        bool includeSubdirectories,
        ISet<string> files,
        ICollection<RejectedPath> rejected,
        CancellationToken cancellationToken)
    {
        var directories = new Stack<string>();
        directories.Push(root);

        while (directories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    AddFile(file, files, rejected);
                }

                if (!includeSubdirectories)
                {
                    continue;
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    directories.Push(child);
                }
            }
            catch (Exception exception)
                when (exception is UnauthorizedAccessException or IOException)
            {
                rejected.Add(new RejectedPath(directory, "无法访问该目录。"));
            }
        }
    }

    private static void AddFile(
        string path,
        ISet<string> files,
        ICollection<RejectedPath> rejected)
    {
        var extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension))
        {
            rejected.Add(new RejectedPath(path, "不支持该文件格式。"));
            return;
        }

        files.Add(Path.GetFullPath(path));
    }
}
