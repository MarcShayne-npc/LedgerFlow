namespace LedgerFlow.Application.Exceptions;

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You are not allowed to perform this action.")
        : base(message) { }
}
