using System.Reflection;
using Application.Common.Interfaces;
using Domain.Customers;
using Domain.PointTransactions;
using Domain.Rewards;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<Reward> Rewards => Set<Reward>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }
}
