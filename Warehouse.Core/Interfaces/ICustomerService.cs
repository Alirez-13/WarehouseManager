using Warehouse.Core.Entities;

namespace Warehouse.Core.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> SearchCustomersAsync(string query, int limit = 10, CancellationToken ct = default);
}