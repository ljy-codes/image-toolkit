using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Options;

public sealed record OutputOptions(
    OutputImageFormat Format,
    OutputMode Mode,
    string? DirectoryPath,
    string FileNameSuffix)
{
    public static OutputOptions Default { get; } =
        new(OutputImageFormat.Original, OutputMode.SourceDirectory, null, "-已处理");
}
