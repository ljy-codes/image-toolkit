namespace ImageToolkit.Domain.Options;

public sealed record CompressionOptions(
    bool Enabled,
    long TargetBytes,
    int MinimumJpegQuality,
    int MinimumWebpQuality,
    double MinimumScaleRatio,
    int MinimumShortEdge,
    bool AllowAutomaticResize,
    bool AllowPngQuantization)
{
    public static CompressionOptions Default { get; } =
        new(true, 1_048_576, 45, 45, 0.25, 320, true, false);
}
