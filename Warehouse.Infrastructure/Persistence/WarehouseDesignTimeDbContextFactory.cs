using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Warehouse.Infrastructure.Persistence;

public class WarehouseDesignTimeDbContextFactory : IDesignTimeDbContextFactory<WarehouseDbContext>
{
    public WarehouseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WarehouseDbContext>();

        // SQLite design-time dummy connection string used only for schema generation
        optionsBuilder.UseSqlite("Data Source=design_time_migrations.db");

        return new WarehouseDbContext(optionsBuilder.Options);
    }
}