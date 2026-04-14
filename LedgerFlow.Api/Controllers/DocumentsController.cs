using LedgerFlow.Application.Documents;
using LedgerFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

    [HttpPost("sales-invoices")]
    public Task<ActionResult<FinancialDocumentResponse>> CreateSales([FromBody] CreateFinancialDocumentRequest request, CancellationToken cancellationToken)
        => Create(request, DocumentType.SalesInvoice, cancellationToken);

    [HttpPost("purchase-invoices")]
    public Task<ActionResult<FinancialDocumentResponse>> CreatePurchase([FromBody] CreateFinancialDocumentRequest request, CancellationToken cancellationToken)
        => Create(request, DocumentType.PurchaseInvoice, cancellationToken);

    private async Task<ActionResult<FinancialDocumentResponse>> Create(CreateFinancialDocumentRequest request, DocumentType type, CancellationToken cancellationToken)
    {
        var doc = await _documents.CreateAsync(request, type, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, doc);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<IReadOnlyList<BulkCreateDocumentResultItem>>> Bulk([FromBody] IReadOnlyList<BulkCreateDocumentItem> items, CancellationToken cancellationToken)
    {
        var result = await _documents.BulkCreateAsync(items, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    public async Task<ActionResult<FinancialDocumentResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _documents.GetAsync(id, cancellationToken);
        return Ok(doc);
    }

    [HttpGet]
    public async Task<ActionResult<LedgerFlow.Application.Common.PagedResult<FinancialDocumentResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] DocumentType? type = null,
        [FromQuery] string? partnerId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _documents.ListAsync(page, pageSize, status, type, partnerId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<FinancialDocumentResponse>> ChangeStatus(
        Guid id,
        [FromBody] ChangeDocumentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var row = Convert.FromBase64String(request.RowVersionBase64);
        var doc = await _documents.ChangeStatusAsync(id, request.Status, row, cancellationToken);
        return Ok(doc);
    }

    [HttpPost("{id:guid}/reprocess")]
    public async Task<ActionResult<FinancialDocumentResponse>> Reprocess(Guid id, [FromBody] ReprocessDocumentRequest request, CancellationToken cancellationToken)
    {
        var doc = await _documents.ReprocessAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, doc);
    }
}

public sealed class ChangeDocumentStatusRequest
{
    public DocumentStatus Status { get; set; }
    public string RowVersionBase64 { get; set; } = string.Empty;
}
