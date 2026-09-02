using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Infrastructure.Files;

public sealed class OutputPathResolver : IOutputPathResolver
{
    public string Resolve(
        string sourcePath,
        OutputOptions options,
        string outputExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode == OutputMode.OverwriteOriginal)
        {
            return Path.GetFullPath(sourcePath);
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath)
            ?? throw new ArgumentException("源文件路径缺少目录。", nameof(sourcePath));
        var outputDirectory = options.Mode == OutputMode.SpecificDirectory
            ? options.DirectoryPath
            : sourceDirectory;

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("输出目录不能为空。", nameof(options));
        }

        Directory.CreateDirectory(outputDirectory);
        var extension = NormalizeExtension(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath) + options.FileNameSuffix;

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

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var normalized = extension.StartsWith('.') ? extension : "." + extension;
        return normalized.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? ".jpg"
            : normalized.ToLowerInvariant();
    }
}
