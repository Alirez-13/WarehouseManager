using Warehouse.Core.Common;

namespace Warehouse.Core.Entities;

public class InboundTransactionLine : BaseEntity
{
    public int InboundTransactionId { get; set; }
    public int? ProductId { get; set; } // Nullable to preserve audit logs if master records are purged

    // Historical Snapshots
    public string ProductSkuSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string UnitSnapshot { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    // Navigation
    public virtual InboundTransaction InboundTransaction { get; set; } = null!;
    public virtual Product? Product { get; set; }
}