using LedgerFlow.Application.Abstractions;
using LedgerFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Infrastructure.Persistence;

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly ApplicationDbContext _db;

    public IdempotencyStore(ApplicationDbContext db) => _db = db;

    public async Task<IdempotencyLookupResult?> TryGetAsync(string key, string requestHash, CancellationToken cancellationToken = default)
    {
        var row = await _db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, cancellationToken);
        if (row is null)
            return null;

        var same = string.Equals(row.RequestHash, requestHash, StringComparison.Ordinal);
        return new IdempotencyLookupResult(row.ResponseStatusCode, row.ResponseBody, same);
    }

    public async Task StoreAsync(string key, string requestHash, int statusCode, string body, CancellationToken cancellationToken = default)
    {
        var existing = await _db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key, cancellationToken);
        if (existing is not null)
        {
            existing.RequestHash = requestHash;
            existing.ResponseStatusCode = statusCode;
            existing.ResponseBody = body;
        }
        else
        {
            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Id = Guid.NewGuid(),
                Key = key,
                RequestHash = requestHash,
                ResponseStatusCode = statusCode,
                ResponseBody = body,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
