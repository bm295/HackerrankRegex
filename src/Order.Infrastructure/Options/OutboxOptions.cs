namespace Order.Infrastructure.Options;

public sealed class OutboxOptions
{
    public int PollIntervalMs { get; set; } = 500;
    public int BatchSize { get; set; } = 50;
}
