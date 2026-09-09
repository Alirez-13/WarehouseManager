namespace Warehouse.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Core.Entities;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku).IsRequired().HasMaxLength(64);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Unit).IsRequired().HasMaxLength(32);
        builder.Property(p => p.Description).HasMaxLength(1000);

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.Name);

        builder.HasOne(p => p.InventoryItem)
            .WithOne(i => i.Product)
            .HasForeignKey<InventoryItem>(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}