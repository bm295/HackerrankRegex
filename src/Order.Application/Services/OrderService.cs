using System.Diagnostics;
using System.Text.Json;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Order.Application.Abstractions;
using Order.Application.Models;
using Order.Domain;
using Shared.Common;

namespace Order.Application.Services;

public class OrderService
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;
    private readonly IOutboxRepository _outbox;
    private readonly ILogger<OrderService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OrderService(IOrderRepository orders, IPaymentRepository payments, IOutboxRepository outbox, ILogger<OrderService> logger)
    {
        _orders = orders;
        _payments = payments;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<Result<OrderResponse>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var items = request.Items.Select(i => new OrderItem(i.Sku, i.Qty, i.Price)).ToList();
        var order = Order.Domain.Order.Create(request.StoreId, items, request.Note);
        await _orders.AddAsync(order, cancellationToken).ConfigureAwait(false);
        await _orders.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var response = new OrderResponse(order.Id, order.StoreId, order.Status, order.CreatedAt, order.UpdatedAt, order.Note,
            order.Items.Select(i => new OrderItemResponse(i.Sku, i.Quantity, i.Price)));
        return Result<OrderResponse>.Ok(response);
    }

    public async Task<Result<PaymentResponse>> RequestPaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result<PaymentResponse>.Fail("order_not_found", $"Order {request.OrderId} not found");
        }

        var existing = await _payments.FindByOrderAndKeyAsync(request.OrderId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            _logger.LogInformation("Idempotent hit for order {OrderId} payment {PaymentId}", request.OrderId, existing.Id);
            return Result<PaymentResponse>.Ok(new PaymentResponse(existing.Id, existing.Status, Activity.Current?.Id ?? string.Empty));
        }

        var payment = order.AddPayment(request.Amount, request.Method, request.IdempotencyKey);
        await _payments.AddAsync(payment, cancellationToken).ConfigureAwait(false);

        var evt = new PaymentRequestedV2(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            payment.OrderId,
            payment.Id,
            payment.Amount,
            payment.Method,
            "USD",
            order.StoreId,
            request.CorrelationId,
            request.TraceParent);

        var payload = JsonSerializer.Serialize(evt, JsonOptions);
        await _outbox.AddAsync("payment.requested.v2", payload, evt.TraceParent, evt.CorrelationId, cancellationToken).ConfigureAwait(false);

        await _orders.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PaymentResponse>.Ok(new PaymentResponse(payment.Id, payment.Status, Activity.Current?.Id ?? string.Empty));
    }

    public async Task<Result<OrderResponse>> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result<OrderResponse>.Fail("order_not_found", $"Order {orderId} not found");
        }

        var response = new OrderResponse(order.Id, order.StoreId, order.Status, order.CreatedAt, order.UpdatedAt, order.Note,
            order.Items.Select(i => new OrderItemResponse(i.Sku, i.Quantity, i.Price)));
        return Result<OrderResponse>.Ok(response);
    }
}
