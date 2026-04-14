using LedgerFlow.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public EventsController(IApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await _db.OutboxEvents.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new { e.Id, e.Type, e.PayloadJson, e.CreatedAt, e.ProcessedAt })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}
