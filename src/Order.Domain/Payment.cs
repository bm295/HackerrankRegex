namespace Order.Domain;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Method { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Payment() { }

    public Payment(Guid id, Guid orderId, decimal amount, string method, string idempotencyKey)
    {
        Id = id;
        OrderId = orderId;
        Amount = amount;
        Method = method;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTime.UtcNow;
        Status = PaymentStatus.Requested;
    }

    public void MarkSucceeded()
    {
        Status = PaymentStatus.Succeeded;
        CompletedAt = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }
}
