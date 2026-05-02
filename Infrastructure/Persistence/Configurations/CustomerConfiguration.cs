using Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(x => x.Value, x => new CustomerId(x));

        builder.Property(x => x.Name).IsRequired().HasColumnType("varchar(255)");
        builder.Property(x => x.Email).IsRequired().HasColumnType("varchar(255)");
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.Phone).IsRequired().HasColumnType("varchar(20)");
        builder.Property(x => x.JoinDate).HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.TotalPoints).HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.TotalEarnedPoints).HasColumnType("integer").HasDefaultValue(0);

        builder.Property(x => x.TierLevel)
            .IsRequired()
            .HasConversion(x => x.ToString(), x => Enum.Parse<TierLevel>(x))
            .HasColumnType("varchar(50)");

        // Optimistic concurrency via Postgres' system column `xmin` so concurrent
        // earn/redeem/expire operations on the same customer detect lost-update conflicts.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
