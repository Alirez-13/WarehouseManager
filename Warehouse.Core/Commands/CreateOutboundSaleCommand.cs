namespace Warehouse.Core.Commands;

public record CreateOutboundSaleCommand(
    string InvoiceNumber,
    string CustomerName,
    string CustomerPhone,
    string? CustomerAddress,
    string? Notes,
    IReadOnlyList<OutboundSaleItemCommand> Items
);
