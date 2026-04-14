using LedgerFlow.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportingController : ControllerBase
{
    private readonly IReportingService _reporting;

    public ReportingController(IReportingService reporting) => _reporting = reporting;

    [HttpGet("reversed")]
    public async Task<IActionResult> Reversed([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _reporting.GetReversedDocumentsAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _reporting.GetActiveTransactionsAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var rows = await _reporting.GetFinancialSummaryAsync(from, to, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("active/export.csv")]
    public async Task<IActionResult> ExportActiveCsv(CancellationToken cancellationToken = default)
    {
        var csv = await _reporting.BuildActiveTransactionsCsvAsync(cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "active-transactions.csv");
    }
}
