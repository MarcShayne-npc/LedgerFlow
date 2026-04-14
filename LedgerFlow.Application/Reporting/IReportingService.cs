using LedgerFlow.Application.Common;
using LedgerFlow.Application.Documents;
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Application.Reporting;

public interface IReportingService
{
    Task<PagedResult<FinancialDocumentResponse>> GetReversedDocumentsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<FinancialDocumentResponse>> GetActiveTransactionsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerSummaryRow>> GetFinancialSummaryAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
    Task<string> BuildActiveTransactionsCsvAsync(CancellationToken cancellationToken = default);
}

public sealed class PartnerSummaryRow
{
    public string? BusinessPartnerId { get; init; }
    public string BusinessPartnerName { get; init; } = string.Empty;
    public decimal NetSignedTotal { get; init; }
}
