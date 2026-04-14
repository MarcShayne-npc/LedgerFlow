using LedgerFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LedgerFlow.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<FinancialDocument> FinancialDocuments { get; }
    DbSet<FinancialDocumentLine> FinancialDocumentLines { get; }
    DbSet<ReversalApproval> ReversalApprovals { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<DocumentNumberSequence> DocumentNumberSequences { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
