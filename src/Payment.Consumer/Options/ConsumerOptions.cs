namespace Payment.Consumer.Options;

public sealed class ConsumerOptions
{
    public string QueueName { get; set; } = "payment.requested.q";
    public string DeadLetterQueue { get; set; } = "payment.requested.dlq";
    public ushort PrefetchCount { get; set; } = 10;
    public int MaxConcurrency { get; set; } = 4;
    public int Capacity => MaxConcurrency * 2;
}
