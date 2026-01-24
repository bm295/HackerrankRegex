namespace Messaging.Contracts;

public abstract record IntegrationMessage(
    Guid MessageId,
    DateTimeOffset OccurredOn,
    string CorrelationId,
    string TraceParent);

public record PaymentRequestedV1(
    Guid MessageId,
    DateTimeOffset OccurredOn,
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Method,
    string CorrelationId,
    string TraceParent) : IntegrationMessage(MessageId, OccurredOn, CorrelationId, TraceParent);

public record PaymentRequestedV2(
    Guid MessageId,
    DateTimeOffset OccurredOn,
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Method,
    string Currency,
    string StoreId,
    string CorrelationId,
    string TraceParent) : IntegrationMessage(MessageId, OccurredOn, CorrelationId, TraceParent);

public record PaymentSucceeded(
    Guid MessageId,
    DateTimeOffset OccurredOn,
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Method,
    string CorrelationId,
    string TraceParent) : IntegrationMessage(MessageId, OccurredOn, CorrelationId, TraceParent);

public record PaymentFailed(
    Guid MessageId,
    DateTimeOffset OccurredOn,
    Guid OrderId,
    Guid PaymentId,
    string Reason,
    string CorrelationId,
    string TraceParent) : IntegrationMessage(MessageId, OccurredOn, CorrelationId, TraceParent);

public record OrderPaid(
    Guid MessageId,
    DateTimeOffset OccurredOn,
    Guid OrderId,
    Guid PaymentId,
    string StoreId,
    string CorrelationId,
    string TraceParent) : IntegrationMessage(MessageId, OccurredOn, CorrelationId, TraceParent);
