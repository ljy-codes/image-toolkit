using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Options;

public sealed record BackgroundOptions(
    BackgroundMode Mode,
    string CustomColor)
{
    public static BackgroundOptions Default { get; } =
        new(BackgroundMode.Preserve, "#FFFFFF");
}
