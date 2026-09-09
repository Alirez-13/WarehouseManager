using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Core.Entities;

namespace Warehouse.Infrastructure.Configurations;

public class InboundTransactionConfiguration : IEntityTypeConfiguration<InboundTransaction>
{
    public void Configure(EntityTypeBuilder<InboundTransaction> builder)
    {
        builder.ToTable("InboundTransactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.ReferenceNumber).IsRequired().HasMaxLength(128);
        builder.Property(t => t.SupplierName).HasMaxLength(256);
        builder.Property(t => t.TotalAmount).HasPrecision(18, 2);

        builder.HasIndex(t => t.ReferenceNumber);
        builder.HasIndex(t => t.TransactionDateUtc);

        builder.HasMany(t => t.Lines)
            .WithOne(l => l.InboundTransaction)
            .HasForeignKey(l => l.InboundTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InboundTransactionLineConfiguration : IEntityTypeConfiguration<InboundTransactionLine>
{
    public void Configure(EntityTypeBuilder<InboundTransactionLine> builder)
    {
        builder.ToTable("InboundTransactionLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ProductSkuSnapshot).IsRequired().HasMaxLength(64);
        builder.Property(l => l.ProductNameSnapshot).IsRequired().HasMaxLength(256);
        builder.Property(l => l.UnitSnapshot).IsRequired().HasMaxLength(32);

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}