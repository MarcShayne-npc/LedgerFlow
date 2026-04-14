using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Domain.Entities;

public class ReversalApproval
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public FinancialDocument Document { get; set; } = null!;

    public ApprovalState State { get; set; }

    public string? RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }

    public string? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    public Guid? ResultingCreditMemoId { get; set; }
    public FinancialDocument? ResultingCreditMemo { get; set; }
}
