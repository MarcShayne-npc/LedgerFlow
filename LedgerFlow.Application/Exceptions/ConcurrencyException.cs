namespace LedgerFlow.Application.Exceptions;

public sealed class ConcurrencyException : AppException
{
    public ConcurrencyException() : base("The record was modified by another user. Refresh and try again.") { }
}
