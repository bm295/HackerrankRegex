namespace Order.Application.Abstractions;

public interface IOutboxRepository
{
    Task AddAsync(string type, string payloadJson, string? traceParent, string? correlationId, CancellationToken cancellationToken);
}
