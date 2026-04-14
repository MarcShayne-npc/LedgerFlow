using LedgerFlow.Application.Abstractions;
using LedgerFlow.Application.Audit;
using LedgerFlow.Application.Common;
using LedgerFlow.Application.Events;
using LedgerFlow.Application.Exceptions;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace LedgerFlow.Application.Documents;

public sealed class DocumentService : IDocumentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public DocumentService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<FinancialDocumentResponse> CreateAsync(CreateFinancialDocumentRequest request, DocumentType documentType, CancellationToken cancellationToken = default)
    {
        DocumentValidator.Validate(request, documentType);
        return await CreateCoreAsync(request, documentType, correctedFromDocumentId: null, cancellationToken);
    }

    public async Task<FinancialDocumentResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FinancialDocuments
            .AsNoTracking()
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
            throw new NotFoundException(nameof(FinancialDocument), id);

        return Map(entity);
    }

    public async Task<PagedResult<FinancialDocumentResponse>> ListAsync(int page, int pageSize, DocumentStatus? status, DocumentType? type, string? partnerId, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.FinancialDocuments.AsNoTracking().Include(d => d.Lines).AsQueryable();

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);
        if (type.HasValue)
            query = query.Where(d => d.DocumentType == type.Value);
        if (!string.IsNullOrWhiteSpace(partnerId))
            query = query.Where(d => d.BusinessPartnerId == partnerId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FinancialDocumentResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(Map).ToList()
        };
    }

    public async Task<IReadOnlyList<BulkCreateDocumentResultItem>> BulkCreateAsync(IReadOnlyList<BulkCreateDocumentItem> items, CancellationToken cancellationToken = default)
    {
        var results = new List<BulkCreateDocumentResultItem>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            try
            {
                DocumentValidator.Validate(item.Payload, item.DocumentType);
                var doc = await CreateCoreAsync(item.Payload, item.DocumentType, correctedFromDocumentId: null, cancellationToken);
                results.Add(new BulkCreateDocumentResultItem { Index = i, Success = true, Document = doc });
            }
            catch (ValidationException vex)
            {
                results.Add(new BulkCreateDocumentResultItem { Index = i, Success = false, Errors = vex.Errors });
            }
        }

        return results;
    }

    public async Task<FinancialDocumentResponse> ChangeStatusAsync(Guid id, DocumentStatus newStatus, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FinancialDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(nameof(FinancialDocument), id);

        entity.RowVersion = rowVersion;
        DocumentValidator.EnsureStatusTransition(entity.Status, newStatus);
        entity.Status = newStatus;
        entity.ModifiedAt = _clock.UtcNow;
        entity.ModifiedByUserId = _currentUser.UserId;

        AuditWriter.Write(_db, nameof(FinancialDocument), entity.Id, "StatusChanged", _currentUser.UserId, _clock.UtcNow, new { entity.Status, newStatus });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException();
        }

        OutboxWriter.Enqueue(_db, "DocumentStatusChanged", new { entity.Id, entity.Status }, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return Map(await ReloadAsync(entity.Id, cancellationToken));
    }

    public async Task<FinancialDocumentResponse> ReprocessAsync(Guid reversedDocumentId, ReprocessDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var original = await _db.FinancialDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == reversedDocumentId, cancellationToken);
        if (original is null)
            throw new NotFoundException(nameof(FinancialDocument), reversedDocumentId);

        if (original.Status != DocumentStatus.Reversed)
            throw new ValidationException(new[]
            {
                new ValidationError("RULE", nameof(reversedDocumentId), "Reprocessing is only allowed for reversed documents.")
            });

        if (original.DocumentType is not (DocumentType.SalesInvoice or DocumentType.PurchaseInvoice))
            throw new ValidationException(new[]
            {
                new ValidationError("RULE", nameof(reversedDocumentId), "Only invoices can be reprocessed.")
            });

        var targetType = original.DocumentType;
        DocumentValidator.Validate(request.Corrected, targetType);

        return await CreateCoreAsync(request.Corrected, targetType, correctedFromDocumentId: reversedDocumentId, cancellationToken);
    }

    private async Task<FinancialDocumentResponse> CreateCoreAsync(
        CreateFinancialDocumentRequest request,
        DocumentType documentType,
        Guid? correctedFromDocumentId,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = _clock.UtcNow;
                var userId = _currentUser.UserId;
                var prefix = documentType switch
                {
                    DocumentType.SalesInvoice => "SI",
                    DocumentType.PurchaseInvoice => "PI",
                    DocumentType.CreditMemo => "CM",
                    _ => "DOC"
                };

                var year = now.Year;
                var seq = await _db.DocumentNumberSequences
                    .FirstOrDefaultAsync(s => s.SeriesPrefix == prefix && s.Year == year, cancellationToken);

                if (seq is null)
                {
                    seq = new DocumentNumberSequence { SeriesPrefix = prefix, Year = year, LastNumber = 0 };
                    _db.DocumentNumberSequences.Add(seq);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                seq.LastNumber++;
                var number = $"{prefix}-{year}-{seq.LastNumber:D6}";

                var doc = new FinancialDocument
                {
                    Id = Guid.NewGuid(),
                    DocumentType = documentType,
                    Status = request.Status,
                    DocumentNumber = number,
                    Currency = request.Currency.Trim().ToUpperInvariant(),
                    BusinessPartnerId = request.BusinessPartnerId,
                    BusinessPartnerName = request.BusinessPartnerName.Trim(),
                    SubTotal = request.SubTotal,
                    TaxAmount = request.TaxAmount,
                    TotalAmount = request.TotalAmount,
                    CorrectedFromDocumentId = correctedFromDocumentId,
                    CreatedAt = now,
                    CreatedByUserId = userId,
                    Lines = request.Lines.Select(l => new FinancialDocumentLine
                    {
                        Id = Guid.NewGuid(),
                        LineNumber = l.LineNumber,
                        ItemCode = l.ItemCode,
                        Description = l.Description.Trim(),
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = l.TaxRate,
                        LineTotal = l.LineTotal
                    }).ToList()
                };

                _db.FinancialDocuments.Add(doc);

                AuditWriter.Write(_db, nameof(FinancialDocument), doc.Id, "Created", userId, now, new { doc.DocumentNumber, doc.DocumentType, doc.TotalAmount });

                var eventType = documentType switch
                {
                    DocumentType.SalesInvoice => "SalesInvoiceCreated",
                    DocumentType.PurchaseInvoice => "PurchaseInvoiceCreated",
                    DocumentType.CreditMemo => "CreditMemoCreated",
                    _ => "DocumentCreated"
                };

                OutboxWriter.Enqueue(_db, eventType, new { doc.Id, doc.DocumentNumber, doc.DocumentType }, now);

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return Map(await ReloadAsync(doc.Id, cancellationToken));
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<FinancialDocument> ReloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.FinancialDocuments.AsNoTracking().Include(d => d.Lines).FirstAsync(d => d.Id == id, cancellationToken);
        return entity;
    }

    private static FinancialDocumentResponse Map(FinancialDocument d) => new()
    {
        Id = d.Id,
        DocumentType = d.DocumentType,
        Status = d.Status,
        DocumentNumber = d.DocumentNumber,
        Currency = d.Currency,
        BusinessPartnerId = d.BusinessPartnerId,
        BusinessPartnerName = d.BusinessPartnerName,
        SubTotal = d.SubTotal,
        TaxAmount = d.TaxAmount,
        TotalAmount = d.TotalAmount,
        OriginalDocumentId = d.OriginalDocumentId,
        ReversalCreditMemoId = d.ReversalCreditMemoId,
        CorrectedFromDocumentId = d.CorrectedFromDocumentId,
        RowVersion = d.RowVersion,
        CreatedAt = d.CreatedAt,
        Lines = d.Lines.OrderBy(l => l.LineNumber).Select(l => new FinancialDocumentLineResponse
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            ItemCode = l.ItemCode,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            TaxRate = l.TaxRate,
            LineTotal = l.LineTotal
        }).ToList()
    };
}
