using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Core.Entities;

namespace Warehouse.Infrastructure.Configurations;

public class OutboundTransactionConfiguration : IEntityTypeConfiguration<OutboundTransaction>
{
    public void Configure(EntityTypeBuilder<OutboundTransaction> builder)
    {
        builder.ToTable("OutboundTransactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.InvoiceNumber).IsRequired().HasMaxLength(128);
        builder.Property(t => t.CustomerNameSnapshot).IsRequired().HasMaxLength(256);
        builder.Property(t => t.CustomerPhoneSnapshot).HasMaxLength(64);
        builder.Property(t => t.TotalAmount).HasPrecision(18, 2);

        builder.HasIndex(t => t.InvoiceNumber).IsUnique();
        builder.HasIndex(t => t.TransactionDateUtc);

        builder.HasOne(t => t.Customer)
            .WithMany(c => c.OutboundTransactions)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Lines)
            .WithOne(l => l.OutboundTransaction)
            .HasForeignKey(l => l.OutboundTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OutboundTransactionLineConfiguration : IEntityTypeConfiguration<OutboundTransactionLine>
{
    public void Configure(EntityTypeBuilder<OutboundTransactionLine> builder)
    {
        builder.ToTable("OutboundTransactionLines");
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