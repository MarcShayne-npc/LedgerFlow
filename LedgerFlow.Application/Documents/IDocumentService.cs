using LedgerFlow.Application.Common;
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Application.Documents;

public interface IDocumentService
{
    Task<FinancialDocumentResponse> CreateAsync(CreateFinancialDocumentRequest request, DocumentType documentType, CancellationToken cancellationToken = default);
    Task<FinancialDocumentResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<FinancialDocumentResponse>> ListAsync(int page, int pageSize, DocumentStatus? status, DocumentType? type, string? partnerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BulkCreateDocumentResultItem>> BulkCreateAsync(IReadOnlyList<BulkCreateDocumentItem> items, CancellationToken cancellationToken = default);
    Task<FinancialDocumentResponse> ChangeStatusAsync(Guid id, DocumentStatus newStatus, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task<FinancialDocumentResponse> ReprocessAsync(Guid reversedDocumentId, ReprocessDocumentRequest request, CancellationToken cancellationToken = default);
}
