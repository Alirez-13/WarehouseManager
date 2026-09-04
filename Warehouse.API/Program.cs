using Warehouse.Api.Middleware;
using Warehouse.Core.Interfaces;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Services;


// using Warehouse.Infrastructure.Services;
// using Warehouse.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HeaderTenantProvider>();

// Singleton Factory manages connection strings, paths, and SQLite Pragmas
builder.Services.AddSingleton<TenantDbContextFactory>();

// Scoped DbContext resolved per HTTP request using the Tenant Header
builder.Services.AddScoped<WarehouseDbContext>(sp =>
{
    var tenantProvider = sp.GetRequiredService<ITenantProvider>();
    var factory = sp.GetRequiredService<TenantDbContextFactory>();
    return factory.CreateDbContext(tenantProvider.GetTenantId());
});

// Domain Services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

var app = builder.Build();

// Custom Concurrency Middleware
app.UseMiddleware<ConcurrencyExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}