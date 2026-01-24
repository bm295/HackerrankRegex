using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly OrderDbContext _dbContext;

    public PaymentRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken) =>
        _dbContext.Payments.AddAsync(payment, cancellationToken).AsTask();

    public Task<Payment?> FindByOrderAndKeyAsync(Guid orderId, string idempotencyKey, CancellationToken cancellationToken) =>
        _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId && p.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
