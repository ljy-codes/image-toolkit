namespace ImageToolkit.Domain.Models;

public sealed record CompressionDecision(
    bool ReachedTarget,
    int Quality,
    long SizeBytes,
    IReadOnlyList<CompressionAttempt> Attempts);
