namespace LedgerFlow.Domain.Entities;

public class FinancialDocumentLine
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public FinancialDocument Document { get; set; } = null!;

    public int LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}
