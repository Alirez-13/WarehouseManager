using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Core.Entities;

namespace Warehouse.Infrastructure.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(256);
        builder.Property(c => c.Phone).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Address).HasMaxLength(500);

        builder.HasIndex(c => c.Phone);
        builder.HasIndex(c => c.Name);
    }
}