using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Models;

public sealed record ImageImportEntry(
    string SourcePath,
    ImportSourceKind SourceKind,
    string SourceRoot,
    string RelativePath)
{
    public static ImageImportEntry FromFile(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        return new ImageImportEntry(
            fullPath,
            ImportSourceKind.File,
            Path.GetDirectoryName(fullPath)
                ?? throw new ArgumentException("源文件路径缺少目录。", nameof(sourcePath)),
            Path.GetFileName(fullPath));
    }

    public static ImageImportEntry FromFolder(
        string sourceRoot,
        string sourcePath)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
        var fullPath = Path.GetFullPath(sourcePath);
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("源文件不在导入文件夹内。", nameof(sourcePath));
        }

        return new ImageImportEntry(
            fullPath,
            ImportSourceKind.Folder,
            fullRoot,
            relativePath);
    }
}
