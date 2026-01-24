namespace Order.Domain;

public class OrderItem
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }

    private OrderItem() { }

    public OrderItem(string sku, int quantity, decimal price)
    {
        Id = Guid.NewGuid();
        Sku = sku;
        Quantity = quantity;
        Price = price;
    }
}
