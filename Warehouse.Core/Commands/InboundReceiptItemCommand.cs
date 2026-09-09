namespace Warehouse.Core.Commands;

public record InboundReceiptItemCommand(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice
);