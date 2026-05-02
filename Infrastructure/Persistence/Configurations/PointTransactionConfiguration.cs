using Domain.Customers;
using Domain.PointTransactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(x => x.Value, x => new PointTransactionId(x));

        builder.Property(x => x.CustomerId)
            .HasConversion(x => x.Value, x => new CustomerId(x));

        builder.Property(x => x.Points).HasColumnType("integer").IsRequired();
        builder.Property(x => x.Remaining).HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.Description).IsRequired().HasColumnType("varchar(500)");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion(x => x.ToString(), x => Enum.Parse<PointTransactionType>(x))
            .HasColumnType("varchar(50)");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .HasConstraintName("fk_point_transactions_customers_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });
        builder.HasIndex(x => new { x.CustomerId, x.Type, x.CreatedAt });
    }
}
