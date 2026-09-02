using ImageToolkit.Domain.Options;

namespace ImageToolkit.Domain.Models;

public sealed record ProcessingRequest(
    CompressionOptions Compression,
    ResizeOptions Resize,
    AspectRatioOptions AspectRatio,
    BackgroundOptions Background,
    MetadataOptions Metadata,
    OutputOptions Output)
{
    public static ProcessingRequest Default { get; } = new(
        CompressionOptions.Default,
        ResizeOptions.Default,
        AspectRatioOptions.Default,
        BackgroundOptions.Default,
        MetadataOptions.Default,
        OutputOptions.Default);
}
