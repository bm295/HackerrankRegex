using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Observability;

public sealed class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIds.HeaderName, out var existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationIds.HeaderName] = correlationId;

        Activity.Current ??= new Activity("incoming-http").Start();
        Activity.Current!.SetIdFormat(ActivityIdFormat.W3C);
        var traceParentHeader = context.Request.Headers.TryGetValue(CorrelationIds.TraceParentHeader, out var traceParentValue)
            ? traceParentValue.ToString()
            : string.Empty;
        Activity.Current!.SetParentId(traceParentHeader);
        Activity.Current!.AddTag("correlation_id", correlationId);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current.TraceId.ToString()
        });

        context.Items[nameof(CorrelationContext)] = new CorrelationContext
        {
            CorrelationId = correlationId,
            TraceParent = Activity.Current.Id ?? string.Empty
        };

        await _next(context).ConfigureAwait(false);
    }
}
