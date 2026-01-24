using Order.Domain;

namespace Order.Application.Models;

public record OrderResponse(Guid OrderId, string StoreId, OrderStatus Status, DateTime CreatedAt, DateTime UpdatedAt, string? Note, IEnumerable<OrderItemResponse> Items);
public record OrderItemResponse(string Sku, int Qty, decimal Price);
public record PaymentResponse(Guid PaymentId, PaymentStatus Status, string TraceId);
