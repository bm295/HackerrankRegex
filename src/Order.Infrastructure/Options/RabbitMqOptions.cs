namespace Order.Infrastructure.Options;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Pass { get; set; } = "guest";
    public string Exchange { get; set; } = "fnb.events";
    public ushort PrefetchCount { get; set; } = 10;
}
