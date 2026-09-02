using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Config;

public sealed record AppConfiguration(
    ProcessingRequest Processing,
    int WorkerCount,
    string Theme,
    bool IncludeSubdirectories)
{
    public static AppConfiguration Default { get; } =
        new(ProcessingRequest.Default, 0, "System", false);
}
