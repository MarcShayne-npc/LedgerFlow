using System.Net.Http.Json;
using System.Text.Json;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LedgerFlow.Infrastructure.Background;

public sealed class OutboxDispatcherJob
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxDispatcherJob> _logger;

    public OutboxDispatcherJob(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OutboxDispatcherJob> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task Dispatch() => DispatchAsync(CancellationToken.None);

    public async Task DispatchAsync(CancellationToken cancellationToken = default)
    {
        var webhook = _configuration["Webhooks:InvoiceEventsUrl"];
        var pending = await _db.OutboxEvents
            .Where(e => e.ProcessedAt == null)
            .OrderBy(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var evt in pending)
        {
            _logger.LogInformation("Outbox event {Type} {Id}: {Payload}", evt.Type, evt.Id, evt.PayloadJson);

            if (!string.IsNullOrWhiteSpace(webhook))
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(15);
                    await client.PostAsJsonAsync(webhook, new { evt.Id, evt.Type, evt.PayloadJson }, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Webhook delivery failed for outbox {Id}", evt.Id);
                    continue;
                }
            }

            evt.ProcessedAt = DateTimeOffset.UtcNow;
        }

        if (pending.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
    }
}
