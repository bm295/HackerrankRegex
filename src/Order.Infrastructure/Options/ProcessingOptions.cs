namespace Order.Infrastructure.Options;

public sealed class ProcessingOptions
{
    public int MaxConcurrency { get; set; } = 4;
}
