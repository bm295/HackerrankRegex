namespace Payment.Consumer.Options;

public sealed class ResilienceOptions
{
    public int RetryCount { get; set; } = 3;
    public int CircuitBreakerFailures { get; set; } = 3;
    public int CircuitBreakerWindowSeconds { get; set; } = 10;
    public int TimeoutMs { get; set; } = 1500;
}
