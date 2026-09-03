namespace ImageToolkit.Domain.Models;

public sealed record ProcessingDiagnostic(
    string Stage,
    string UserMessage,
    string? TechnicalMessage,
    long? TargetBytes,
    long? BestAttemptBytes,
    IReadOnlyList<string> Suggestions);
