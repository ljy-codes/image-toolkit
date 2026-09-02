namespace ImageToolkit.Domain.Options;

public sealed record MetadataOptions(
    bool PreserveExif,
    bool PreserveGps,
    bool PreserveIccProfile,
    bool ConvertToSrgbWhenIccCannotBePreserved)
{
    public static MetadataOptions Default { get; } =
        new(true, false, true, true);
}
