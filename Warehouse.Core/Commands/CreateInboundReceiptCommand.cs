namespace Warehouse.Core.Commands;

public record CreateInboundReceiptCommand(
    string ReferenceNumber,
    string? SupplierName,
    string? Notes,
    IReadOnlyList<InboundReceiptItemCommand> Items
);