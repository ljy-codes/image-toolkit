namespace ImageToolkit.Domain.Results;

public sealed record ValidationResult
{
    public ValidationResult(IEnumerable<ValidationError> errors)
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
