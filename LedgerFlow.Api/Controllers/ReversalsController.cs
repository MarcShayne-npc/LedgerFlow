using LedgerFlow.Application.Reversals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
public sealed class ReversalsController : ControllerBase
{
    private readonly IReversalService _reversals;

    public ReversalsController(IReversalService reversals) => _reversals = reversals;

    [HttpPost("api/documents/{documentId:guid}/reversals/requests")]
    public async Task<ActionResult<ReversalApprovalResponse>> RequestReversal(Guid documentId, CancellationToken cancellationToken)
    {
        var result = await _reversals.RequestReversalAsync(documentId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/reversals/{approvalId:guid}/approve")]
    public async Task<ActionResult<ReversalApprovalResponse>> Approve(Guid approvalId, CancellationToken cancellationToken)
    {
        var result = await _reversals.ApproveAsync(approvalId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/reversals/{approvalId:guid}/execute")]
    public async Task<ActionResult<ReversalApprovalResponse>> Execute(Guid approvalId, CancellationToken cancellationToken)
    {
        var result = await _reversals.ExecuteAsync(approvalId, cancellationToken);
        return Ok(result);
    }
}
