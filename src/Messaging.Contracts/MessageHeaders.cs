namespace Messaging.Contracts;

public static class MessageHeaders
{
    public const string TraceParent = "traceparent";
    public const string CorrelationId = "x-correlation-id";
    public const string MessageId = "message-id";
    public const string MessageType = "message-type";
}
