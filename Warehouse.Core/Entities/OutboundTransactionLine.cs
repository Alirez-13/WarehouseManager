using Warehouse.Core.Common;

namespace Warehouse.Core.Entities;

public class OutboundTransactionLine : BaseEntity
{
    public int OutboundTransactionId { get; set; }
    public int? ProductId { get; set; }

    // Historical Snapshots
    public string ProductSkuSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string UnitSnapshot { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    // Navigation
    public virtual OutboundTransaction OutboundTransaction { get; set; } = null!;
    public virtual Product? Product { get; set; }
}