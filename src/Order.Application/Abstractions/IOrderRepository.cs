using Order.Domain;

namespace Order.Application.Abstractions;

public interface IOrderRepository
{
    Task AddAsync(Order.Domain.Order order, CancellationToken cancellationToken);
    Task<Order.Domain.Order?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
