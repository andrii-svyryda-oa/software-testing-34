using Application.Common.Interfaces.Queries;
using Domain.Rewards;
using MediatR;

namespace Application.Rewards.Queries;

public record GetAvailableRewardsQuery : IRequest<IReadOnlyList<Reward>>;

public class GetAvailableRewardsQueryHandler(IRewardQueries rewardQueries)
    : IRequestHandler<GetAvailableRewardsQuery, IReadOnlyList<Reward>>
{
    public Task<IReadOnlyList<Reward>> Handle(GetAvailableRewardsQuery request, CancellationToken cancellationToken)
        => rewardQueries.GetAvailable(cancellationToken);
}
