namespace Shared.Observability;

public sealed class CorrelationContext
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public string TraceParent { get; init; } = string.Empty;
}
