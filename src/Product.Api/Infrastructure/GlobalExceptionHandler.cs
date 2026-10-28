using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Product.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning("Request cancelled by client");
            context.Response.StatusCode = 499;
            return true;
        }

        var status = exception is ApiException api ? api.StatusCode : 500;
        var detail = exception is ApiException ? exception.Message : "An unexpected error occurred. Use the correlation ID when reporting this issue.";
        if (status >= 500)
            logger.LogError("Request failed with {StatusCode}; exception type {ExceptionType}", status, exception.GetType().Name);
        else
            logger.LogWarning("Request rejected with {StatusCode}: {Reason}", status, detail);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status switch { 400 => "Invalid request", 404 => "Not found", 409 => "Insufficient stock", 503 => "Product Service unavailable", _ => "Server error" },
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        return true;
    }
}
