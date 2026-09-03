namespace ImageToolkit.Domain.Models;

public sealed record AiModelStatus(
    string ModelId,
    string DisplayName,
    long SizeBytes,
    bool IsInstalled,
    string License);
