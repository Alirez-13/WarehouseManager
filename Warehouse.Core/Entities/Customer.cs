namespace Warehouse.Core.Entities;

using Warehouse.Core.Common;

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ICollection<OutboundTransaction> OutboundTransactions { get; set; } = new List<OutboundTransaction>();
}