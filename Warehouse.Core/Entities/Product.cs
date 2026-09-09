namespace Warehouse.Core.Entities;

using Common;

public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty; // e.g., "pcs", "kg", "box"
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual InventoryItem? InventoryItem { get; set; }
}

public class InventoryItem
{
    public int ProductId { get; set; }
    public decimal QuantityOnHand { get; set; }

    // SQLite concurrency token regenerated on every update
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();
    public DateTime LastStockUpdateUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Product Product { get; set; } = null!;
}