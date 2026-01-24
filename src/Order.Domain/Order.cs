namespace Order.Domain;

public class Order
{
    private readonly List<OrderItem> _items = new();

    public Guid Id { get; private set; }
    public string StoreId { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
    public Payment? Payment { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public static Order Create(string storeId, IEnumerable<OrderItem> items, string? note)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Status = OrderStatus.Created,
            Note = note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        order._items.AddRange(items);
        return order;
    }

    public Payment AddPayment(decimal amount, string method, string idempotencyKey)
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Order cancelled");
        }

        var payment = new Payment(Guid.NewGuid(), Id, amount, method, idempotencyKey);
        Payment = payment;
        Status = OrderStatus.PendingPayment;
        UpdatedAt = DateTime.UtcNow;
        return payment;
    }

    public void MarkPaid(Guid paymentId)
    {
        if (Payment is null || Payment.Id != paymentId)
        {
            return;
        }

        Payment.MarkSucceeded();
        Status = OrderStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaymentFailed(Guid paymentId, string reason)
    {
        if (Payment is null || Payment.Id != paymentId)
        {
            return;
        }

        Payment.MarkFailed(reason);
        if (Status != OrderStatus.Paid)
        {
            Status = OrderStatus.Cancelled;
        }

        UpdatedAt = DateTime.UtcNow;
    }
}
