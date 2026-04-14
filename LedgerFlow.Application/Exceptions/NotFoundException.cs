namespace LedgerFlow.Application.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string name, object key)
        : base($"{name} ({key}) was not found.") { }
}
