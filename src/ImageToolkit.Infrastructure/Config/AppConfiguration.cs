using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Config;

public sealed record AppConfiguration(
    ProcessingRequest Processing,
    int WorkerCount,
    string Theme,
    bool IncludeSubdirectories,
    string WorkspaceBackground = "System",
    string CustomWorkspaceColor = "#F3F5F7",
    double FontSize = 14)
{
    public static AppConfiguration Default { get; } =
        new(ProcessingRequest.Default, 0, "System", false);
}
