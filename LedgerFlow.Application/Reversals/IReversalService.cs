namespace LedgerFlow.Application.Reversals;

public interface IReversalService
{
    Task<ReversalApprovalResponse> RequestReversalAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<ReversalApprovalResponse> ApproveAsync(Guid approvalId, CancellationToken cancellationToken = default);
    Task<ReversalApprovalResponse> ExecuteAsync(Guid approvalId, CancellationToken cancellationToken = default);
}

public sealed class ReversalApprovalResponse
{
    public Guid Id { get; init; }
    public Guid DocumentId { get; init; }
    public string State { get; init; } = string.Empty;
    public Guid? ResultingCreditMemoId { get; init; }
}
