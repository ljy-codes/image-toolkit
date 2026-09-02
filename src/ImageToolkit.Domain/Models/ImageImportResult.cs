namespace ImageToolkit.Domain.Models;

public sealed record ImageImportResult(
    IReadOnlyList<string> Files,
    IReadOnlyList<RejectedPath> Rejected);
