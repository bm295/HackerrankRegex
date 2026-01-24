using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Order.Application.Models;
using Order.Application.Services;
using Order.Infrastructure;
using Shared.Common;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCorrelationAndLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOrderInfrastructure(builder.Configuration);
builder.Services.AddScoped<OrderService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.Migrate();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/v1/orders", async ([FromBody] CreateOrderRequest request, OrderService service, HttpContext ctx, CancellationToken ct) =>
{
    var result = await service.CreateOrderAsync(request, ct).ConfigureAwait(false);
    if (result.IsSuccess)
    {
        return Results.Created($"/api/v1/orders/{result.Value!.OrderId}", result.Value);
    }

    return TypedResults.BadRequest(ToError(result.Error!, ctx));
});

app.MapPost("/api/v1/orders/{orderId:guid}/payments", HandlePaymentAsync);

app.MapGet("/api/v1/orders/{orderId:guid}", async (Guid orderId, OrderService service, HttpContext ctx, CancellationToken ct) =>
{
    var result = await service.GetOrderAsync(orderId, ct).ConfigureAwait(false);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : TypedResults.NotFound(ToError(result.Error!, ctx));
});

app.MapGet("/api/v1/health", async (OrderDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct).ConfigureAwait(false);
    return canConnect ? Results.Ok(new { status = "ok" }) : Results.Problem("Database unavailable");
});

app.MapGet("/api/v1/demo/anti-patterns", async (CancellationToken ct) =>
{
    var data = await AntiPatternDemo.RunAsync(ct).ConfigureAwait(false);
    return Results.Ok(data);
});

app.MapPost("/api/v1/external/psp", async ([FromBody] ExternalChargeRequest request, CancellationToken ct) =>
{
    var jitter = Random.Shared.Next(50, 120);
    await Task.Delay(jitter, ct).ConfigureAwait(false);
    var succeed = Random.Shared.NextDouble() > 0.3;
    return succeed ? Results.Ok(new { status = "ok" }) : Results.StatusCode(502);
});

static async Task<IResult> HandlePaymentAsync(
    Guid orderId,
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
    [FromBody] PaymentInput body,
    OrderService service,
    HttpContext ctx,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return TypedResults.BadRequest(ToError(new Error("idempotency_required", "Idempotency-Key header required"), ctx));
    }

    var correlationId = ctx.Request.Headers.TryGetValue(CorrelationIds.HeaderName, out var cid)
        ? cid.ToString()
        : Guid.NewGuid().ToString("N");

    var traceParent = Activity.Current?.Id ?? ctx.TraceIdentifier;

    var request = new CreatePaymentRequest(orderId, body.Amount, body.Method, idempotencyKey, correlationId, traceParent);
    var result = await service.RequestPaymentAsync(request, ct).ConfigureAwait(false);

    if (result.IsSuccess)
    {
        return TypedResults.Ok(result.Value);
    }

    return TypedResults.BadRequest(ToError(result.Error!, ctx));
}

app.Run();

static object ToError(Error error, HttpContext ctx)
{
    return new
    {
        traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier,
        error = new
        {
            code = error.Code,
            message = error.Message,
            details = error.Details
        }
    };
}

internal record PaymentInput(decimal Amount, string Method);
internal record ExternalChargeRequest(Guid OrderId, Guid PaymentId, decimal Amount);

public partial class Program { }
