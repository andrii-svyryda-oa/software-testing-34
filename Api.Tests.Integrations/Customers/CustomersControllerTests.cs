using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Domain.Customers;
using Domain.PointTransactions;
using Domain.Rewards;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.Data.Builders;
using Tests.Common;
using Xunit;

namespace Api.Tests.Integrations.Customers;

[Collection("IntegrationTests")]
public class CustomersControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private Customer _alice = null!;

    public CustomersControllerTests(IntegrationTestWebFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await ClearAllAsync();
        _alice = CustomersData.Bronze("alice@x.io");
        await Context.Customers.AddAsync(_alice);
        await SaveChangesAsync();
    }

    public async Task DisposeAsync() => await ClearAllAsync();

    private async Task ClearAllAsync()
    {
        await Context.PointTransactions.ExecuteDeleteAsync();
        await Context.Customers.ExecuteDeleteAsync();
        await Context.Rewards.ExecuteDeleteAsync();
        Context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Register_returns_201_and_persists()
    {
        var dto = new RegisterCustomerDto("Bob Smith", "bob@x.io", "+380501112233");

        var response = await Client.PostAsJsonAsync("/api/customers", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ToResponseModel<CustomerDto>();
        body.Email.Should().Be("bob@x.io");
        body.TierLevel.Should().Be(TierLevel.Bronze);
        body.TotalPoints.Should().Be(0);

        var inDb = await Context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Email == "bob@x.io");
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409()
    {
        var dto = new RegisterCustomerDto("Duplicate", "alice@x.io", "+380501112233");
        var response = await Client.PostAsJsonAsync("/api/customers", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", "bob@x.io", "+380501112233")]
    [InlineData("Bob", "not-an-email", "+380501112233")]
    [InlineData("Bob", "bob@x.io", "12")]
    public async Task Register_with_invalid_payload_returns_400(string name, string email, string phone)
    {
        var dto = new RegisterCustomerDto(name, email, phone);
        var response = await Client.PostAsJsonAsync("/api/customers", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_existing_customer_returns_200_with_dto()
    {
        var response = await Client.GetAsync($"/api/customers/{_alice.Id.Value}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ToResponseModel<CustomerDto>();
        body.Id.Should().Be(_alice.Id.Value);
        body.Email.Should().Be("alice@x.io");
    }

    [Fact]
    public async Task Get_nonexistent_customer_returns_404()
    {
        var response = await Client.GetAsync($"/api/customers/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Earn_below_silver_threshold_keeps_bronze()
    {
        var earnResponse = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/earn",
            new EarnPointsDto(500, null));
        earnResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var customer = await Context.Customers.AsNoTracking().FirstAsync(x => x.Id == _alice.Id);
        customer.TotalPoints.Should().Be(500);
        customer.TotalEarnedPoints.Should().Be(500);
        customer.TierLevel.Should().Be(TierLevel.Bronze);
    }

    [Fact]
    public async Task Earn_crossing_silver_threshold_promotes_tier()
    {
        await Client.PostAsJsonAsync($"/api/customers/{_alice.Id.Value}/earn", new EarnPointsDto(500, null));
        await Client.PostAsJsonAsync($"/api/customers/{_alice.Id.Value}/earn", new EarnPointsDto(600, null));

        var customer = await Context.Customers.AsNoTracking().FirstAsync(x => x.Id == _alice.Id);
        customer.TotalEarnedPoints.Should().Be(1100);
        customer.TierLevel.Should().Be(TierLevel.Silver);
    }

    [Fact]
    public async Task Earn_uses_gold_multiplier()
    {
        // Bring Alice to Gold without multiplier via direct AwardBonus (no API for this).
        var alice = await Context.Customers.FirstAsync(x => x.Id == _alice.Id);
        alice.AwardBonus(6_000, DateTime.UtcNow);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/earn",
            new EarnPointsDto(1000, null));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await Context.Customers.AsNoTracking().FirstAsync(x => x.Id == _alice.Id);
        refreshed.TotalEarnedPoints.Should().Be(7_500);
        refreshed.TierLevel.Should().Be(TierLevel.Gold);

        var earnedTxs = await Context.PointTransactions.AsNoTracking()
            .Where(x => x.CustomerId == _alice.Id && x.Type == PointTransactionType.Earned)
            .ToListAsync();
        earnedTxs.Should().HaveCount(1);
        earnedTxs[0].Points.Should().Be(1500);
    }

    [Fact]
    public async Task Earn_uses_platinum_multiplier()
    {
        var alice = await Context.Customers.FirstAsync(x => x.Id == _alice.Id);
        alice.AwardBonus(11_000, DateTime.UtcNow);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/earn",
            new EarnPointsDto(1000, null));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await Context.Customers.AsNoTracking().FirstAsync(x => x.Id == _alice.Id);
        refreshed.TotalEarnedPoints.Should().Be(13_000);
        refreshed.TierLevel.Should().Be(TierLevel.Platinum);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Earn_with_non_positive_returns_400(int basePoints)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/earn",
            new EarnPointsDto(basePoints, null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Redeem_succeeds_and_decrements_stock()
    {
        var alice = await Context.Customers.FirstAsync(x => x.Id == _alice.Id);
        alice.AwardBonus(500, DateTime.UtcNow);
        // Need an Earned/Bonus transaction with Remaining=500 for the redeem flow.
        var deposit = PointTransactionsData.Bonus(_alice.Id, 500, DateTime.UtcNow);
        await Context.PointTransactions.AddAsync(deposit);

        var reward = RewardsData.Catalog(pointsCost: 200, stock: 3);
        await Context.Rewards.AddAsync(reward);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/redeem",
            new RedeemPointsDto(reward.Id.Value));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await Context.Customers.AsNoTracking().FirstAsync(x => x.Id == _alice.Id);
        refreshed.TotalPoints.Should().Be(300);

        var refreshedReward = await Context.Rewards.AsNoTracking().FirstAsync(x => x.Id == reward.Id);
        refreshedReward.StockQuantity.Should().Be(2);

        var redeemedTxs = await Context.PointTransactions.AsNoTracking()
            .Where(x => x.CustomerId == _alice.Id && x.Type == PointTransactionType.Redeemed)
            .ToListAsync();
        redeemedTxs.Should().HaveCount(1);
        redeemedTxs[0].Points.Should().Be(200);
    }

    [Fact]
    public async Task Redeem_insufficient_points_returns_422()
    {
        var alice = await Context.Customers.FirstAsync(x => x.Id == _alice.Id);
        alice.AwardBonus(100, DateTime.UtcNow);
        var deposit = PointTransactionsData.Bonus(_alice.Id, 100, DateTime.UtcNow);
        await Context.PointTransactions.AddAsync(deposit);

        var reward = RewardsData.Catalog(pointsCost: 10_000, stock: 5);
        await Context.Rewards.AddAsync(reward);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/redeem",
            new RedeemPointsDto(reward.Id.Value));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Redeem_out_of_stock_returns_422()
    {
        var alice = await Context.Customers.FirstAsync(x => x.Id == _alice.Id);
        alice.AwardBonus(1_000, DateTime.UtcNow);
        await Context.PointTransactions.AddAsync(PointTransactionsData.Bonus(_alice.Id, 1_000, DateTime.UtcNow));

        var reward = RewardsData.OutOfStock(pointsCost: 100);
        await Context.Rewards.AddAsync(reward);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/redeem",
            new RedeemPointsDto(reward.Id.Value));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Redeem_inactive_reward_returns_422()
    {
        var alice = await Context.Customers.FirstAsync(x => x.Id == _alice.Id);
        alice.AwardBonus(1_000, DateTime.UtcNow);
        await Context.PointTransactions.AddAsync(PointTransactionsData.Bonus(_alice.Id, 1_000, DateTime.UtcNow));

        var reward = RewardsData.Inactive(pointsCost: 100);
        await Context.Rewards.AddAsync(reward);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/redeem",
            new RedeemPointsDto(reward.Id.Value));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Redeem_unknown_reward_returns_404()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/customers/{_alice.Id.Value}/redeem",
            new RedeemPointsDto(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_returns_paginated_newest_first()
    {
        var baseTime = DateTime.UtcNow.AddDays(-30);
        var txs = Enumerable.Range(0, 25)
            .Select(i => PointTransactionsData.Earned(_alice.Id, 10, baseTime.AddMinutes(i)))
            .ToList();
        await Context.PointTransactions.AddRangeAsync(txs);
        await SaveChangesAsync();

        var page1 = await Client.GetAsync($"/api/customers/{_alice.Id.Value}/history?page=1&pageSize=10");
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1Body = await page1.ToResponseModel<PaginatedData<PointTransactionDto>>();
        page1Body.Total.Should().Be(25);
        page1Body.Data.Should().HaveCount(10);
        page1Body.Data[0].CreatedAt.Should().BeAfter(page1Body.Data[^1].CreatedAt);

        var page3 = await Client.GetAsync($"/api/customers/{_alice.Id.Value}/history?page=3&pageSize=10");
        var page3Body = await page3.ToResponseModel<PaginatedData<PointTransactionDto>>();
        page3Body.Data.Should().HaveCount(5);
    }

    [Fact]
    public async Task Tier_returns_progress()
    {
        var alice = await Context.Customers.FirstAsync(x => x.Id == _alice.Id);
        alice.AwardBonus(2_500, DateTime.UtcNow);
        await SaveChangesAsync();

        var response = await Client.GetAsync($"/api/customers/{_alice.Id.Value}/tier");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ToResponseModel<CustomerTierDto>();
        body.Current.Should().Be(TierLevel.Silver);
        body.Next.Should().Be(TierLevel.Gold);
        body.TotalEarnedPoints.Should().Be(2_500);
        body.PointsToNext.Should().Be(2_500);
    }
}
