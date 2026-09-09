using Warehouse.Core.Commands;

namespace Warehouse.Core.Interfaces;

public interface IInventoryService
{
    Task ProcessInboundReceiptAsync(CreateInboundReceiptCommand command, CancellationToken ct = default);
    Task ProcessOutboundSaleAsync(CreateOutboundSaleCommand command, CancellationToken ct);
}