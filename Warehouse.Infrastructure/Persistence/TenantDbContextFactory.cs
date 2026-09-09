namespace Warehouse.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

public class TenantDbContextFactory
{
    private readonly string _databaseDirectory;
    private static readonly ConcurrentDictionary<string, bool> InitializedTenants = new();

    public TenantDbContextFactory(IConfiguration configuration)
    {
        // Allow appsettings.json to configure path, falling back to a local folder
        _databaseDirectory = configuration["TenantSettings:DatabaseDirectory"]
                             ?? Path.Combine(AppContext.BaseDirectory, "TenantData");

        if (!Directory.Exists(_databaseDirectory))
        {
            Directory.CreateDirectory(_databaseDirectory);
        }
    }

    public WarehouseDbContext CreateDbContext(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        // Prevent path traversal attacks (e.g., "../../bad")
        var safeTenantId = Path.GetFileName(tenantId.Trim());
        var dbPath = Path.Combine(_databaseDirectory, $"{safeTenantId}.db");

        // High-performance SQLite configuration
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,            // Enforce FK constraints
            DefaultTimeout = 30           // 30-second busy timeout for concurrent transactions
        };

        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseSqlite(connectionStringBuilder.ToString())
            .Options;

        var context = new WarehouseDbContext(options);

        // Ensure WAL mode and apply schema migrations for new tenant databases
        EnsureInitialized(context, safeTenantId);

        return context;
    }

    private static void EnsureInitialized(WarehouseDbContext context, string tenantId)
    {
        InitializedTenants.GetOrAdd(tenantId, _ =>
        {
            var connection = context.Database.GetDbConnection();
            connection.Open();

            // Enable WAL mode (Write-Ahead Logging) for non-blocking reads/writes
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                command.ExecuteNonQuery();
            }

            // Automatically apply EF Core migrations if the tenant file is brand new
            context.Database.Migrate();

            return true;
        });
    }
}