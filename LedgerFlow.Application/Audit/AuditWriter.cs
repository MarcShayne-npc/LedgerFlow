using System.Text.Json;
using LedgerFlow.Application.Abstractions;
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Application.Audit;

public static class AuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static void Write(
        IApplicationDbContext db,
        string entityName,
        Guid entityId,
        string action,
        string? userId,
        DateTimeOffset occurredAt,
        object payload)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            UserId = userId,
            OccurredAt = occurredAt,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions)
        });
    }
}
