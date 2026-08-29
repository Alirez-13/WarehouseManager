using Warehouse.Core.Common;

namespace Warehouse.Core.Entities;

public class InboundTransaction : BaseEntity
{
    public string ReferenceNumber { get; set; } = string.Empty; // e.g., PO number / Bill of Lading
    public string? SupplierName { get; set; }
    public DateTime TransactionDateUtc { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public virtual ICollection<InboundTransactionLine> Lines { get; set; } = new List<InboundTransactionLine>();
}