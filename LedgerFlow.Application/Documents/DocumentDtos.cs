using LedgerFlow.Application.Exceptions;
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Application.Documents;

public sealed class FinancialDocumentLineRequest
{
    public int LineNumber { get; init; }
    public string? ItemCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TaxRate { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class CreateFinancialDocumentRequest
{
    public DocumentType? DocumentType { get; init; }
    public string Currency { get; init; } = "USD";
    public string? BusinessPartnerId { get; init; }
    public string BusinessPartnerName { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public DocumentStatus Status { get; init; } = DocumentStatus.Open;
    public IReadOnlyList<FinancialDocumentLineRequest> Lines { get; init; } = Array.Empty<FinancialDocumentLineRequest>();
}

public sealed class FinancialDocumentLineResponse
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public string? ItemCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TaxRate { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class FinancialDocumentResponse
{
    public Guid Id { get; init; }
    public DocumentType DocumentType { get; init; }
    public DocumentStatus Status { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string? BusinessPartnerId { get; init; }
    public string BusinessPartnerName { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public Guid? OriginalDocumentId { get; init; }
    public Guid? ReversalCreditMemoId { get; init; }
    public Guid? CorrectedFromDocumentId { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyList<FinancialDocumentLineResponse> Lines { get; init; } = Array.Empty<FinancialDocumentLineResponse>();
}

public sealed class BulkCreateDocumentItem
{
    public DocumentType DocumentType { get; init; }
    public CreateFinancialDocumentRequest Payload { get; init; } = new();
}

public sealed class BulkCreateDocumentResultItem
{
    public int Index { get; init; }
    public bool Success { get; init; }
    public FinancialDocumentResponse? Document { get; init; }
    public IReadOnlyList<ValidationError>? Errors { get; init; }
}

public sealed class ReprocessDocumentRequest
{
    public CreateFinancialDocumentRequest Corrected { get; init; } = new();
}
