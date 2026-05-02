using Application.Common.Interfaces.Queries;
using Domain.PointTransactions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Data.Builders;
using Tests.Common;
using Xunit;

namespace Api.Tests.Integrations.Database;

[Collection("IntegrationTests")]
public class TransactionHistoryIntegrityTests : BaseIntegrationTest, IAsyncLifetime
{
    public TransactionHistoryIntegrityTests(IntegrationTestWebFactory factory) : base(factory) { }

    public async Task InitializeAsync() => await ClearAllAsync();
    public async Task DisposeAsync() => await ClearAllAsync();

    private async Task ClearAllAsync()
    {
        await Context.PointTransactions.ExecuteDeleteAsync();
        await Context.Customers.ExecuteDeleteAsync();
        await Context.Rewards.ExecuteDeleteAsync();
        Context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Foreign_key_cascade_on_customer_delete()
    {
        var customer = CustomersData.Bronze();
        await Context.Customers.AddAsync(customer);
        for (var i = 0; i < 5; i++)
            await Context.PointTransactions.AddAsync(
                PointTransactionsData.Earned(customer.Id, 10, DateTime.UtcNow.AddMinutes(-i)));
        await SaveChangesAsync();

        var tracked = await Context.Customers.FirstAsync(c => c.Id == customer.Id);
        Context.Customers.Remove(tracked);
        await SaveChangesAsync();

        var remaining = await Context.PointTransactions
            .CountAsync(t => t.CustomerId == customer.Id);
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task Inserting_transaction_with_unknown_customer_id_fails()
    {
        var orphan = PointTransactionsData.Earned(
            new Domain.Customers.CustomerId(Guid.NewGuid()), 10);
        await Context.PointTransactions.AddAsync(orphan);

        var act = async () => await Context.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.And.InnerException!.Message.Should().Contain("fk_point_transactions_customers_id");
    }

    [Fact]
    public async Task History_query_orders_newest_first()
    {
        var customer = CustomersData.Bronze();
        await Context.Customers.AddAsync(customer);
        var baseTime = DateTime.UtcNow.AddDays(-1);
        for (var i = 0; i < 10; i++)
            await Context.PointTransactions.AddAsync(
                PointTransactionsData.Earned(customer.Id, 10, baseTime.AddMinutes(i)));
        await SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPointTransactionQueries>();
        var history = await queries.GetHistoryFor(customer.Id, skip: 0, take: 100, default);

        history.Should().HaveCount(10);
        history.Should().BeInDescendingOrder(t => t.CreatedAt);
    }
}
