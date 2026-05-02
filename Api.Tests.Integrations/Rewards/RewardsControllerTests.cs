using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Domain.Rewards;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.Data.Builders;
using Tests.Common;
using Xunit;

namespace Api.Tests.Integrations.Rewards;

[Collection("IntegrationTests")]
public class RewardsControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    public RewardsControllerTests(IntegrationTestWebFactory factory) : base(factory) { }

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
    public async Task List_returns_only_active_in_stock_rewards()
    {
        var active = RewardsData.Catalog(pointsCost: 100, stock: 5);
        var noStock = RewardsData.OutOfStock(pointsCost: 200);
        var inactive = RewardsData.Inactive(pointsCost: 300);
        await Context.Rewards.AddRangeAsync(active, noStock, inactive);
        await SaveChangesAsync();

        var response = await Client.GetAsync("/api/rewards");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ToResponseModel<List<RewardDto>>();

        body.Should().HaveCount(1);
        body[0].Id.Should().Be(active.Id.Value);
    }

    [Fact]
    public async Task List_orders_by_name()
    {
        var b = Reward.Create(RewardId.New(), "Bbb thing", "x", 100, "cat", 1);
        var a = Reward.Create(RewardId.New(), "Aaa thing", "x", 100, "cat", 1);
        var c = Reward.Create(RewardId.New(), "Ccc thing", "x", 100, "cat", 1);
        await Context.Rewards.AddRangeAsync(b, c, a);
        await SaveChangesAsync();

        var response = await Client.GetAsync("/api/rewards");
        var body = await response.ToResponseModel<List<RewardDto>>();
        body.Select(x => x.Name).Should().ContainInOrder("Aaa thing", "Bbb thing", "Ccc thing");
    }

    [Fact]
    public async Task Create_returns_201_and_persists()
    {
        var dto = new CreateRewardDto("New Reward", "A nice reward", 250, "Toys", 10);

        var response = await Client.PostAsJsonAsync("/api/rewards", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ToResponseModel<RewardDto>();
        body.Name.Should().Be("New Reward");
        body.PointsCost.Should().Be(250);
        body.IsActive.Should().BeTrue();

        var rewardId = new RewardId(body.Id);
        var inDb = await Context.Rewards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rewardId);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_with_negative_cost_returns_400()
    {
        var dto = new CreateRewardDto("Bad", "desc", -1, "cat", 10);
        var response = await Client.PostAsJsonAsync("/api/rewards", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_zero_stock_is_allowed_and_excluded_from_list()
    {
        var dto = new CreateRewardDto("Pre-order", "A future reward", 100, "Toys", 0);

        var createResponse = await Client.PostAsJsonAsync("/api/rewards", dto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.ToResponseModel<RewardDto>();

        var rewardId = new RewardId(created.Id);
        var inDb = await Context.Rewards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rewardId);
        inDb.Should().NotBeNull();

        var listResponse = await Client.GetAsync("/api/rewards");
        var list = await listResponse.ToResponseModel<List<RewardDto>>();
        list.Should().NotContain(x => x.Id == created.Id);
    }
}
