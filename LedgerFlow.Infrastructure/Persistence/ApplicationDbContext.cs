using LedgerFlow.Application.Abstractions;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Infrastructure.Persistence;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<FinancialDocument> FinancialDocuments => Set<FinancialDocument>();
    public DbSet<FinancialDocumentLine> FinancialDocumentLines => Set<FinancialDocumentLine>();
    public DbSet<ReversalApproval> ReversalApprovals => Set<ReversalApproval>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<DocumentNumberSequence> DocumentNumberSequences => Set<DocumentNumberSequence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FinancialDocument>(e =>
        {
            e.ToTable("FinancialDocuments");
            e.HasKey(x => x.Id);
            e.Property(x => x.DocumentNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.BusinessPartnerName).HasMaxLength(256).IsRequired();
            e.Property(x => x.BusinessPartnerId).HasMaxLength(128);
            e.Property(x => x.SubTotal).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();

            e.HasOne(x => x.OriginalDocument)
                .WithMany()
                .HasForeignKey(x => x.OriginalDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ReversalCreditMemo)
                .WithMany()
                .HasForeignKey(x => x.ReversalCreditMemoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CorrectedFromDocument)
                .WithMany()
                .HasForeignKey(x => x.CorrectedFromDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<FinancialDocumentLine>(e =>
        {
            e.ToTable("FinancialDocumentLines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(512).IsRequired();
            e.Property(x => x.ItemCode).HasMaxLength(128);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.UnitPrice).HasPrecision(18, 4);
            e.Property(x => x.TaxRate).HasPrecision(9, 4);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);

            e.HasOne(x => x.Document)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReversalApproval>(e =>
        {
            e.ToTable("ReversalApprovals");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Document)
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ResultingCreditMemo)
                .WithMany()
                .HasForeignKey(x => x.ResultingCreditMemoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditEntry>(e =>
        {
            e.ToTable("AuditEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityName).HasMaxLength(128).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Action).HasMaxLength(128).IsRequired();
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.PayloadJson).IsRequired();
            e.HasIndex(x => new { x.EntityName, x.EntityId });
        });

        builder.Entity<OutboxEvent>(e =>
        {
            e.ToTable("OutboxEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(128).IsRequired();
            e.Property(x => x.PayloadJson).IsRequired();
            e.HasIndex(x => x.ProcessedAt);
        });

        builder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("IdempotencyRecords");
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(128).IsRequired();
            e.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.ResponseBody).IsRequired();
            e.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<DocumentNumberSequence>(e =>
        {
            e.ToTable("DocumentNumberSequences");
            e.HasKey(x => x.Id);
            e.Property(x => x.SeriesPrefix).HasMaxLength(16).IsRequired();
            e.HasIndex(x => new { x.SeriesPrefix, x.Year }).IsUnique();
        });
    }
}
