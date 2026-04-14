using System.Text.Json;
using LedgerFlow.Application.Abstractions;
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Application.Events;

public static class OutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static void Enqueue(IApplicationDbContext db, string type, object payload, DateTimeOffset createdAt)
    {
        db.OutboxEvents.Add(new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            CreatedAt = createdAt
        });
    }
}
