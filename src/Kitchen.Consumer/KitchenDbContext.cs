using Microsoft.EntityFrameworkCore;
using Kitchen.Consumer.Storage;

namespace Kitchen.Consumer;

public class KitchenDbContext : DbContext
{
    public KitchenDbContext(DbContextOptions<KitchenDbContext> options) : base(options)
    {
    }

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(i => new { i.MessageId, i.Consumer });
        });
    }
}
