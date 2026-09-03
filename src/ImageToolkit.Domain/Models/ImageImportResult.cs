namespace ImageToolkit.Domain.Models;

public sealed record ImageImportResult(
    IReadOnlyList<string> Files,
    IReadOnlyList<RejectedPath> Rejected,
    IReadOnlyList<ImageImportEntry>? SourceEntries = null)
{
    public IReadOnlyList<ImageImportEntry> Entries { get; } =
        SourceEntries ?? Files.Select(ImageImportEntry.FromFile).ToArray();
}
