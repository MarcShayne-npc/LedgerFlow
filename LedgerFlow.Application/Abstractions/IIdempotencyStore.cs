namespace LedgerFlow.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<IdempotencyLookupResult?> TryGetAsync(string key, string requestHash, CancellationToken cancellationToken = default);
    Task StoreAsync(string key, string requestHash, int statusCode, string body, CancellationToken cancellationToken = default);
}

public sealed record IdempotencyLookupResult(int StatusCode, string Body, bool SamePayload);
