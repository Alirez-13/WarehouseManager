namespace Warehouse.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Core.Entities;

public class WarehouseDbContext : DbContext
{
    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<InboundTransaction> InboundTransactions => Set<InboundTransaction>();
    public DbSet<InboundTransactionLine> InboundTransactionLines => Set<InboundTransactionLine>();
    public DbSet<OutboundTransaction> OutboundTransactions => Set<OutboundTransaction>();
    public DbSet<OutboundTransactionLine> OutboundTransactionLines => Set<OutboundTransactionLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WarehouseDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<InventoryItem>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.ConcurrencyVersion = Guid.NewGuid();
                entry.Entity.LastStockUpdateUtc = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}