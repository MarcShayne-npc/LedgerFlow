using System.Net;
using System.Text.Json;
using LedgerFlow.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace LedgerFlow.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, ex);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, problem) = ex switch
        {
            ValidationException vex => (HttpStatusCode.BadRequest, new ProblemDetails
            {
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
                Detail = vex.Message,
                Extensions = { ["errors"] = vex.Errors }
            }),
            NotFoundException nf => (HttpStatusCode.NotFound, new ProblemDetails
            {
                Title = "Not found",
                Status = StatusCodes.Status404NotFound,
                Detail = nf.Message
            }),
            ForbiddenException fb => (HttpStatusCode.Forbidden, new ProblemDetails
            {
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = fb.Message
            }),
            ConcurrencyException => (HttpStatusCode.Conflict, new ProblemDetails
            {
                Title = "Concurrency conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = ex.Message
            }),
            _ => (HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Server error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred."
            })
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
