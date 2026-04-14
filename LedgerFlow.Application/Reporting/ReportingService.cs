using System.Globalization;
using System.Text;
using LedgerFlow.Application.Abstractions;
using LedgerFlow.Application.Common;
using LedgerFlow.Application.Documents;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Application.Reporting;

public sealed class ReportingService : IReportingService
{
    private readonly IApplicationDbContext _db;

    public ReportingService(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<FinancialDocumentResponse>> GetReversedDocumentsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.FinancialDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.Status == DocumentStatus.Reversed);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(d => d.ModifiedAt ?? d.CreatedAt)
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

    public async Task<PagedResult<FinancialDocumentResponse>> GetActiveTransactionsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.FinancialDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.Status == DocumentStatus.Open || d.Status == DocumentStatus.Closed);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(d => d.CreatedAt)
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

    public async Task<IReadOnlyList<PartnerSummaryRow>> GetFinancialSummaryAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var query = _db.FinancialDocuments.AsNoTracking()
            .Where(d =>
                d.DocumentType == DocumentType.SalesInvoice ||
                d.DocumentType == DocumentType.PurchaseInvoice ||
                d.DocumentType == DocumentType.CreditMemo)
            .Where(d => d.Status != DocumentStatus.Cancelled);

        if (from.HasValue)
            query = query.Where(d => d.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(d => d.CreatedAt <= to.Value);

        var docs = await query.ToListAsync(cancellationToken);

        var rows = docs
            .GroupBy(d => new { d.BusinessPartnerId, d.BusinessPartnerName })
            .Select(g => new PartnerSummaryRow
            {
                BusinessPartnerId = g.Key.BusinessPartnerId,
                BusinessPartnerName = g.Key.BusinessPartnerName,
                NetSignedTotal = g.Sum(d =>
                    d.DocumentType == DocumentType.CreditMemo ? d.TotalAmount :
                    d.DocumentType == DocumentType.PurchaseInvoice ? -d.TotalAmount :
                    d.TotalAmount)
            })
            .OrderByDescending(r => r.NetSignedTotal)
            .ToList();

        return rows;
    }

    public async Task<string> BuildActiveTransactionsCsvAsync(CancellationToken cancellationToken = default)
    {
        var items = await _db.FinancialDocuments.AsNoTracking()
            .Where(d => d.Status == DocumentStatus.Open || d.Status == DocumentStatus.Closed)
            .OrderByDescending(d => d.CreatedAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Id,DocumentNumber,Type,Status,Partner,Total,CreatedAt");
        foreach (var d in items)
        {
            sb.AppendLine(string.Join(",",
                Csv(d.Id.ToString()),
                Csv(d.DocumentNumber),
                Csv(d.DocumentType.ToString()),
                Csv(d.Status.ToString()),
                Csv(d.BusinessPartnerName),
                d.TotalAmount.ToString(CultureInfo.InvariantCulture),
                Csv(d.CreatedAt.ToString("O"))));
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        if (value is null) return "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
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
