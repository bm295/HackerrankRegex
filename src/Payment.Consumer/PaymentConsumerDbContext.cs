using Microsoft.EntityFrameworkCore;
using Payment.Consumer.Storage;

namespace Payment.Consumer;

public class PaymentConsumerDbContext : DbContext
{
    public PaymentConsumerDbContext(DbContextOptions<PaymentConsumerDbContext> options) : base(options)
    {
    }

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<PaymentOutboxMessage> OutboxMessages => Set<PaymentOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(i => new { i.MessageId, i.Consumer });
        });

        modelBuilder.Entity<PaymentOutboxMessage>(entity =>
        {
            entity.ToTable("PaymentOutboxMessages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).HasMaxLength(200);
            entity.Property(o => o.Payload).IsRequired();
        });
    }
}
