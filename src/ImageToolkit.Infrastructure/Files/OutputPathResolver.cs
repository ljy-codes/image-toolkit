using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Infrastructure.Files;

public sealed class OutputPathResolver : IOutputPathResolver
{
    public string Resolve(
        string sourcePath,
        OutputOptions options,
        string outputExtension) =>
        Resolve(ImageImportEntry.FromFile(sourcePath), options, outputExtension);

    public string Resolve(
        ImageImportEntry entry,
        OutputOptions options,
        string outputExtension)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode == OutputMode.OverwriteOriginal)
        {
            return entry.SourcePath;
        }

        var outputDirectory = ResolveOutputDirectory(entry, options);

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("输出目录不能为空。", nameof(options));
        }

        Directory.CreateDirectory(outputDirectory);
        var extension = NormalizeExtension(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(entry.SourcePath);
        if (entry.SourceKind == ImportSourceKind.File ||
            options.Mode == OutputMode.SpecificDirectory)
        {
            baseName += options.FileNameSuffix;
        }

        for (var index = 1; ; index++)
        {
            var suffix = index == 1 ? string.Empty : $"-{index}";
            var candidate = Path.Combine(outputDirectory, baseName + suffix + extension);

            try
            {
                using var reservation = new FileStream(
                    candidate,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // Another worker or an existing file owns this name.
            }
        }
    }

    private static string? ResolveOutputDirectory(
        ImageImportEntry entry,
        OutputOptions options)
    {
        if (options.Mode == OutputMode.SpecificDirectory)
        {
            if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            {
                return options.DirectoryPath;
            }

            var relativeDirectory = entry.SourceKind == ImportSourceKind.Folder
                ? Path.GetDirectoryName(entry.RelativePath)
                : null;
            return string.IsNullOrWhiteSpace(relativeDirectory)
                ? options.DirectoryPath
                : Path.Combine(options.DirectoryPath, relativeDirectory);
        }

        if (entry.SourceKind == ImportSourceKind.File)
        {
            return Path.GetDirectoryName(entry.SourcePath);
        }

        var parent = Path.GetDirectoryName(entry.SourceRoot)
            ?? throw new ArgumentException("导入文件夹缺少父目录。", nameof(entry));
        var processedRoot = Path.Combine(
            parent,
            Path.GetFileName(entry.SourceRoot) + "-已处理");
        var relativeDirectoryPath = Path.GetDirectoryName(entry.RelativePath);
        return string.IsNullOrWhiteSpace(relativeDirectoryPath)
            ? processedRoot
            : Path.Combine(processedRoot, relativeDirectoryPath);
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var normalized = extension.StartsWith('.') ? extension : "." + extension;
        return normalized.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? ".jpg"
            : normalized.ToLowerInvariant();
    }
}
