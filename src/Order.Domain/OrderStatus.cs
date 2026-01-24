namespace Order.Domain;

public enum OrderStatus
{
    Created = 0,
    PendingPayment = 1,
    Paid = 2,
    Cancelled = 3
}
