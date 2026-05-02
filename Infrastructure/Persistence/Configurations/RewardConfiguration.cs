using Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RewardConfiguration : IEntityTypeConfiguration<Reward>
{
    public void Configure(EntityTypeBuilder<Reward> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(x => x.Value, x => new RewardId(x));

        builder.Property(x => x.Name).IsRequired().HasColumnType("varchar(255)");
        builder.Property(x => x.Description).IsRequired().HasColumnType("varchar(1000)");
        builder.Property(x => x.Category).IsRequired().HasColumnType("varchar(50)");
        builder.Property(x => x.PointsCost).HasColumnType("integer").IsRequired();
        builder.Property(x => x.StockQuantity).HasColumnType("integer").IsRequired();
        builder.Property(x => x.IsActive).HasColumnType("boolean").HasDefaultValue(true);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.IsActive);
    }
}
