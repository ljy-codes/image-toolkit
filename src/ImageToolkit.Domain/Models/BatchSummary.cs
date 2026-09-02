using ImageToolkit.Domain.Enums;

namespace ImageToolkit.Domain.Models;

public sealed record BatchSummary(
    BatchRunState State,
    int Total,
    int Completed,
    int Unmet,
    int Failed,
    int Cancelled);
