using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Domain.Entities;

public class FinancialDocument
{
    public Guid Id { get; set; }
    public DocumentType DocumentType { get; set; }
    public DocumentStatus Status { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string? BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>For credit memos: the invoice being reversed.</summary>
    public Guid? OriginalDocumentId { get; set; }
    public FinancialDocument? OriginalDocument { get; set; }

    /// <summary>Credit memo created when this document was reversed.</summary>
    public Guid? ReversalCreditMemoId { get; set; }
    public FinancialDocument? ReversalCreditMemo { get; set; }

    /// <summary>Corrected replacement points to the prior document (e.g. reversed invoice).</summary>
    public Guid? CorrectedFromDocumentId { get; set; }
    public FinancialDocument? CorrectedFromDocument { get; set; }

    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ModifiedByUserId { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }

    public ICollection<FinancialDocumentLine> Lines { get; set; } = new List<FinancialDocumentLine>();
}
