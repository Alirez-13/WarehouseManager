using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Core.Entities;

namespace Warehouse.Infrastructure.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasKey(i => i.ProductId);

        builder.Property(i => i.QuantityOnHand)
            .HasPrecision(18, 4)
            .IsRequired();

        // Optimistic Concurrency Token
        builder.Property(i => i.ConcurrencyVersion)
            .IsConcurrencyToken()
            .IsRequired();
    }
}