namespace ImageToolkit.Domain.Options;

public sealed record ResizeOptions(
    bool Enabled,
    int? Width,
    int? Height,
    bool LockAspectRatio,
    bool AllowUpscale)
{
    public static ResizeOptions Default { get; } =
        new(false, null, null, true, false);
}
