namespace Shared.Observability;

public static class CorrelationIds
{
    public const string HeaderName = "X-Correlation-Id";
    public const string TraceParentHeader = "traceparent";
}
