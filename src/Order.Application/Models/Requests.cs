namespace Order.Application.Models;

public record CreateOrderRequest(string StoreId, IReadOnlyCollection<OrderItemRequest> Items, string? Note);
public record OrderItemRequest(string Sku, int Qty, decimal Price);

public record CreatePaymentRequest(Guid OrderId, decimal Amount, string Method, string IdempotencyKey, string CorrelationId, string TraceParent);
