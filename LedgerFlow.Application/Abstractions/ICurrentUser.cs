namespace LedgerFlow.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsInRole(string role);
}
