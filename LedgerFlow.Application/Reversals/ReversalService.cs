using LedgerFlow.Application.Abstractions;
using LedgerFlow.Application.Audit;
using LedgerFlow.Application.Common;
using LedgerFlow.Application.Events;
using LedgerFlow.Application.Exceptions;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Application.Reversals;

public sealed class ReversalService : IReversalService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public ReversalService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ReversalApprovalResponse> RequestReversalAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        EnsureReversalRole();

        var doc = await _db.FinancialDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (doc is null)
            throw new NotFoundException(nameof(FinancialDocument), documentId);

        if (doc.DocumentType == DocumentType.CreditMemo)
            throw new ValidationException(new[] { new ValidationError("RULE", nameof(documentId), "Credit memos cannot be reversed via this flow.") });

        if (doc.Status is DocumentStatus.Reversed or DocumentStatus.Cancelled)
            throw new ValidationException(new[] { new ValidationError("RULE", nameof(documentId), "Document is not eligible for reversal.") });

        var pending = await _db.ReversalApprovals.AnyAsync(
            a => a.DocumentId == documentId && a.State == ApprovalState.Pending,
            cancellationToken);

        if (pending)
            throw new ValidationException(new[] { new ValidationError("RULE", nameof(documentId), "A pending reversal approval already exists for this document.") });

        var approval = new ReversalApproval
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            State = ApprovalState.Pending,
            RequestedByUserId = _currentUser.UserId,
            RequestedAt = _clock.UtcNow
        };

        _db.ReversalApprovals.Add(approval);

        AuditWriter.Write(_db, nameof(ReversalApproval), approval.Id, "ReversalRequested", _currentUser.UserId, _clock.UtcNow, new { documentId });
        OutboxWriter.Enqueue(_db, "ReversalRequested", new { approval.Id, documentId }, _clock.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);

        return Map(approval);
    }

    public async Task<ReversalApprovalResponse> ApproveAsync(Guid approvalId, CancellationToken cancellationToken = default)
    {
        EnsureReversalRole();

        var approval = await _db.ReversalApprovals.FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
        if (approval is null)
            throw new NotFoundException(nameof(ReversalApproval), approvalId);

        if (approval.State != ApprovalState.Pending)
            throw new ValidationException(new[] { new ValidationError("RULE", nameof(approvalId), "Only pending approvals can be approved.") });

        approval.State = ApprovalState.Approved;
        approval.ApprovedByUserId = _currentUser.UserId;
        approval.ApprovedAt = _clock.UtcNow;

        AuditWriter.Write(_db, nameof(ReversalApproval), approval.Id, "ReversalApproved", _currentUser.UserId, _clock.UtcNow, new { approval.DocumentId });
        OutboxWriter.Enqueue(_db, "ReversalApproved", new { approval.Id, approval.DocumentId }, _clock.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);

        return Map(approval);
    }

    public async Task<ReversalApprovalResponse> ExecuteAsync(Guid approvalId, CancellationToken cancellationToken = default)
    {
        EnsureReversalRole();

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var approval = await _db.ReversalApprovals.FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
                if (approval is null)
                    throw new NotFoundException(nameof(ReversalApproval), approvalId);

                if (approval.State != ApprovalState.Approved)
                    throw new ValidationException(new[] { new ValidationError("RULE", nameof(approvalId), "Only approved reversals can be executed.") });

                var doc = await _db.FinancialDocuments.Include(d => d.Lines).FirstAsync(d => d.Id == approval.DocumentId, cancellationToken);

                if (doc.Status is DocumentStatus.Reversed or DocumentStatus.Cancelled)
                    throw new ValidationException(new[] { new ValidationError("RULE", nameof(approval.DocumentId), "Document is no longer eligible for reversal.") });

                var now = _clock.UtcNow;
                var userId = _currentUser.UserId;

                var prefix = "CM";
                var year = now.Year;
                var seq = await _db.DocumentNumberSequences.FirstOrDefaultAsync(s => s.SeriesPrefix == prefix && s.Year == year, cancellationToken);
                if (seq is null)
                {
                    seq = new DocumentNumberSequence { SeriesPrefix = prefix, Year = year, LastNumber = 0 };
                    _db.DocumentNumberSequences.Add(seq);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                seq.LastNumber++;
                var number = $"{prefix}-{year}-{seq.LastNumber:D6}";

                var creditLines = doc.Lines.OrderBy(l => l.LineNumber).Select(l =>
                {
                    var qty = -l.Quantity;
                    var unit = l.UnitPrice;
                    var tax = l.TaxRate;
                    var net = qty * unit;
                    var taxPart = Math.Round(net * (tax / 100m), 2, MidpointRounding.AwayFromZero);
                    var lineTotal = Math.Round(net + taxPart, 2, MidpointRounding.AwayFromZero);
                    return new FinancialDocumentLine
                    {
                        Id = Guid.NewGuid(),
                        LineNumber = l.LineNumber,
                        ItemCode = l.ItemCode,
                        Description = l.Description,
                        Quantity = qty,
                        UnitPrice = unit,
                        TaxRate = tax,
                        LineTotal = lineTotal
                    };
                }).ToList();

                var subTotal = Math.Round(creditLines.Sum(l => l.Quantity * l.UnitPrice), 2, MidpointRounding.AwayFromZero);
                var taxAmount = Math.Round(creditLines.Sum(l =>
                {
                    var net = l.Quantity * l.UnitPrice;
                    return Math.Round(net * (l.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
                }), 2, MidpointRounding.AwayFromZero);
                var total = Math.Round(creditLines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);

                var credit = new FinancialDocument
                {
                    Id = Guid.NewGuid(),
                    DocumentType = DocumentType.CreditMemo,
                    Status = DocumentStatus.Closed,
                    DocumentNumber = number,
                    Currency = doc.Currency,
                    BusinessPartnerId = doc.BusinessPartnerId,
                    BusinessPartnerName = doc.BusinessPartnerName,
                    SubTotal = subTotal,
                    TaxAmount = taxAmount,
                    TotalAmount = total,
                    OriginalDocumentId = doc.Id,
                    CreatedAt = now,
                    CreatedByUserId = userId,
                    Lines = creditLines
                };

                _db.FinancialDocuments.Add(credit);

                doc.Status = DocumentStatus.Reversed;
                doc.ReversalCreditMemoId = credit.Id;
                doc.ModifiedAt = now;
                doc.ModifiedByUserId = userId;

                approval.State = ApprovalState.Executed;
                approval.ResultingCreditMemoId = credit.Id;

                AuditWriter.Write(_db, nameof(FinancialDocument), credit.Id, "CreditMemoCreated", userId, now, new { credit.DocumentNumber, OriginalDocumentId = doc.Id });
                AuditWriter.Write(_db, nameof(FinancialDocument), doc.Id, "DocumentReversed", userId, now, new { credit.Id });
                AuditWriter.Write(_db, nameof(ReversalApproval), approval.Id, "ReversalExecuted", userId, now, new { credit.Id });

                OutboxWriter.Enqueue(_db, "InvoiceReversed", new { OriginalDocumentId = doc.Id, CreditMemoId = credit.Id }, now);
                OutboxWriter.Enqueue(_db, "CreditMemoCreated", new { credit.Id, credit.DocumentNumber }, now);

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new ConcurrencyException();
                }

                await tx.CommitAsync(cancellationToken);

                var reloaded = await _db.ReversalApprovals.AsNoTracking().FirstAsync(a => a.Id == approvalId, cancellationToken);
                return Map(reloaded);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private void EnsureReversalRole()
    {
        if (!_currentUser.IsInRole(Roles.Admin) && !_currentUser.IsInRole(Roles.Accountant))
            throw new ForbiddenException("Only Admin or Accountant can perform reversal operations.");
    }

    private static ReversalApprovalResponse Map(ReversalApproval a) => new()
    {
        Id = a.Id,
        DocumentId = a.DocumentId,
        State = a.State.ToString(),
        ResultingCreditMemoId = a.ResultingCreditMemoId
    };
}
