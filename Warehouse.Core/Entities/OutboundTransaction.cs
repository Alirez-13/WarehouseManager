using Warehouse.Core.Common;

namespace Warehouse.Core.Entities;

public class OutboundTransaction : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }

    // Historical Customer Snapshot
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string? CustomerPhoneSnapshot { get; set; }

    public DateTime TransactionDateUtc { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public virtual Customer Customer { get; set; } = null!;
    public virtual ICollection<OutboundTransactionLine> Lines { get; set; } = new List<OutboundTransactionLine>();
}