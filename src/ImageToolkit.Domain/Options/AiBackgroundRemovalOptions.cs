using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Options;

public sealed record AiBackgroundRemovalOptions(BackgroundRemovalMode Mode)
{
    public static AiBackgroundRemovalOptions Default { get; } =
        new(BackgroundRemovalMode.Disabled);
}
