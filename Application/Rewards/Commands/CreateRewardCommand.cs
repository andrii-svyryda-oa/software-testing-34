using Application.Common;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Repositories;
using Application.Rewards.Exceptions;
using Domain.Rewards;
using MediatR;

namespace Application.Rewards.Commands;

public record CreateRewardCommand : IRequest<Result<Reward, RewardException>>
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int PointsCost { get; init; }
    public required string Category { get; init; }
    public required int StockQuantity { get; init; }
}

public class CreateRewardCommandHandler(
    IRewardRepository rewardRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRewardCommand, Result<Reward, RewardException>>
{
    public async Task<Result<Reward, RewardException>> Handle(
        CreateRewardCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var reward = Reward.Create(
                RewardId.New(),
                request.Name,
                request.Description,
                request.PointsCost,
                request.Category,
                request.StockQuantity);

            await rewardRepository.Add(reward, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return reward;
        }
        catch (Exception ex)
        {
            return new RewardUnknownException(RewardId.Empty(), ex);
        }
    }
}
