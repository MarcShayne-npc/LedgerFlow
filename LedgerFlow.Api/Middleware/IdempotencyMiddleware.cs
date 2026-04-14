using System.Security.Cryptography;
using System.Text;
using LedgerFlow.Application.Abstractions;

namespace LedgerFlow.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly PathString[] Prefixes =
    [
        new("/api/documents")
    ];

    public IdempotencyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        if (context.Request.Method != HttpMethods.Post || !ShouldApply(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted);
        }

        context.Request.Body.Position = 0;

        var hash = ComputeHash(body);

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var existing = await store.TryGetAsync(key, hash, context.RequestAborted);
        if (existing is not null)
        {
            if (!existing.SamePayload)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Idempotency key conflict",
                    status = 409,
                    detail = "The Idempotency-Key was reused with a different request body."
                }, context.RequestAborted);
                return;
            }

            context.Response.StatusCode = existing.StatusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(existing.Body, context.RequestAborted);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await _next(context);

        buffer.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(buffer).ReadToEndAsync(context.RequestAborted);

        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(originalBody, context.RequestAborted);
        context.Response.Body = originalBody;

        if (context.Response.StatusCode is >= 200 and < 300)
        {
            using var storeScope = scopeFactory.CreateScope();
            var store2 = storeScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            await store2.StoreAsync(key, hash, context.Response.StatusCode, responseText, context.RequestAborted);
        }
    }

    private static bool ShouldApply(PathString path)
    {
        foreach (var p in Prefixes)
        {
            if (path.StartsWithSegments(p))
                return true;
        }

        return false;
    }

    private static string ComputeHash(string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(bytes);
    }
}
