namespace Warehouse.Core.Commands;

public record OutboundSaleItemCommand(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice
);