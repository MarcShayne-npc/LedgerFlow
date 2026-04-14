namespace LedgerFlow.Application.Exceptions;

public sealed class ValidationException : AppException
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

public sealed record ValidationError(string Code, string Field, string Message);
