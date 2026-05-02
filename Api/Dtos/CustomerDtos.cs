using Application.Customers.Queries;
using Domain.Customers;

namespace Api.Dtos;

public record CustomerDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    TierLevel TierLevel,
    int TotalPoints,
    int TotalEarnedPoints,
    DateTime JoinDate)
{
    public static CustomerDto FromDomainModel(Customer c) => new(
        c.Id.Value, c.Name, c.Email, c.Phone,
        c.TierLevel, c.TotalPoints, c.TotalEarnedPoints, c.JoinDate);
}

public record RegisterCustomerDto(string Name, string Email, string Phone);

public record EarnPointsDto(int BasePoints, string? Description);

public record RedeemPointsDto(Guid RewardId);

public record CustomerTierDto(
    TierLevel Current,
    TierLevel Next,
    int TotalEarnedPoints,
    int PointsToNext)
{
    public static CustomerTierDto FromResult(CustomerTierResult r) =>
        new(r.Current, r.Next, r.TotalEarnedPoints, r.PointsToNext);
}
