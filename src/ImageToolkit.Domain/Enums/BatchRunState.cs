namespace ImageToolkit.Domain.Enums;

public enum BatchRunState
{
    Idle,
    Running,
    Paused,
    Cancelling,
    Completed,
    CompletedWithIssues,
    Cancelled
}
