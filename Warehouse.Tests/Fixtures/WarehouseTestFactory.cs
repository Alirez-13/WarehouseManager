using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace Warehouse.Tests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Infrastructure.Persistence;

// Explicitly use the global or namespace-qualified Program from the API project
public class WarehouseTestFactory : WebApplicationFactory<Program>
{
    public string TestDatabaseDirectory { get; } = Path.Combine(Path.GetTempPath(), "WarehouseTests_" + Guid.NewGuid());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantSettings:DatabaseDirectory"] = TestDatabaseDirectory
            });
        });
    }

    public WarehouseDbContext CreateTestDbContext(string tenantId)
    {
        var factory = new TenantDbContextFactory(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantSettings:DatabaseDirectory"] = TestDatabaseDirectory
            }).Build());

        return factory.CreateDbContext(tenantId);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(TestDatabaseDirectory))
        {
            try
            {
                Directory.Delete(TestDatabaseDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        GC.SuppressFinalize(this);
    }
}