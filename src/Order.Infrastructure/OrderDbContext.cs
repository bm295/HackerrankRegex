using Microsoft.EntityFrameworkCore;
using Order.Domain;
using Order.Infrastructure.Entities;

namespace Order.Infrastructure;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order.Domain.Order> Orders => Set<Order.Domain.Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order.Domain.Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.StoreId).IsRequired().HasMaxLength(100);
            entity.Property(o => o.Status).IsRequired();
            entity.Property(o => o.RowVersion).IsRowVersion();
            entity.Property(o => o.CreatedAt).IsRequired();
            entity.Property(o => o.UpdatedAt).IsRequired();

            entity.OwnsMany(o => o.Items, items =>
            {
                items.ToTable("OrderItems");
                items.WithOwner().HasForeignKey("OrderId");
                items.Property<Guid>("Id");
                items.HasKey("Id");
                items.Property(i => i.Sku).HasMaxLength(50).IsRequired();
                items.Property(i => i.Quantity).IsRequired();
                items.Property(i => i.Price).HasColumnType("decimal(18,2)");
            });

            entity.HasOne(o => o.Payment)
                .WithOne()
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Method).HasMaxLength(50).IsRequired();
            entity.Property(p => p.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            entity.HasIndex(p => new { p.OrderId, p.IdempotencyKey }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).HasMaxLength(200).IsRequired();
            entity.Property(o => o.Payload).IsRequired();
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(i => new { i.MessageId, i.Consumer });
        });
    }
}
