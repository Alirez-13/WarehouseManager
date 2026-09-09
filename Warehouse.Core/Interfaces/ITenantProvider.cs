namespace Warehouse.Core.Interfaces;

public interface ITenantProvider
{
    string GetTenantId();
}