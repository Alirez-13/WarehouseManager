namespace Warehouse.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Core.Entities;
using Core.Interfaces;
using Persistence;

public class CustomerService : ICustomerService
{
    private readonly WarehouseDbContext _context;

    public CustomerService(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Customer>> SearchCustomersAsync(string query, int limit = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Customer>();
        }

        var normalized = query.Trim();

        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.IsActive && (EF.Functions.Like(c.Name, $"%{normalized}%") ||
                                       EF.Functions.Like(c.Phone, $"{normalized}%")))
            .OrderBy(c => c.Name)
            .Take(limit)
            .ToListAsync(ct);
    }
}