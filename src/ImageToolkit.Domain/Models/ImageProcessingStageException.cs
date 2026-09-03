namespace ImageToolkit.Domain.Models;

public sealed class ImageProcessingStageException : Exception
{
    public ImageProcessingStageException(
        string errorCode,
        ProcessingDiagnostic diagnostic,
        Exception innerException)
        : base(diagnostic.UserMessage, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ErrorCode = errorCode;
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public string ErrorCode { get; }

    public ProcessingDiagnostic Diagnostic { get; }
}
