using Order.Application.Abstractions;
using Order.Infrastructure.Entities;

namespace Order.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly OrderDbContext _dbContext;

    public OutboxRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(string type, string payloadJson, string? traceParent, string? correlationId, CancellationToken cancellationToken)
    {
        var entity = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            Payload = payloadJson,
            Type = type,
            TraceParent = traceParent,
            CorrelationId = correlationId
        };

        return _dbContext.OutboxMessages.AddAsync(entity, cancellationToken).AsTask();
    }
}
