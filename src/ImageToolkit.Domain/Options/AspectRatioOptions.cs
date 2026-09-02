using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Options;

public sealed record AspectRatioOptions(
    AspectRatioMode Mode,
    int RatioWidth,
    int RatioHeight,
    CropAnchor CropAnchor)
{
    public static AspectRatioOptions Default { get; } =
        new(AspectRatioMode.Original, 1, 1, CropAnchor.Center);
}
