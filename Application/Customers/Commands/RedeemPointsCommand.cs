using Application.Common;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Application.Customers.Exceptions;
using Domain.Customers;
using Domain.PointTransactions;
using Domain.Rewards;
using MediatR;

namespace Application.Customers.Commands;

public record RedeemPointsCommand : IRequest<Result<Customer, CustomerException>>
{
    public required Guid CustomerId { get; init; }
    public required Guid RewardId { get; init; }
}

/// <summary>
/// Redeems points against a reward. Validates balance + reward stock, walks the customer's
/// active Earned/Bonus transactions oldest-first to <see cref="PointTransaction.Consume"/>
/// the cost, decrements reward stock, and writes a <see cref="PointTransaction.Redeemed"/>
/// record &mdash; all in a single <see cref="IUnitOfWork.SaveChangesAsync"/> for atomicity.
/// <para>
/// Concurrency on scarce rewards is handled via an <c>xmin</c> concurrency token configured
/// on <see cref="Reward"/> in the persistence layer.
/// </para>
/// </summary>
public class RedeemPointsCommandHandler(
    ICustomerQueries customerQueries,
    IRewardRepository rewardRepository,
    ICustomerRepository customerRepository,
    IPointTransactionRepository transactionRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RedeemPointsCommand, Result<Customer, CustomerException>>
{
    public async Task<Result<Customer, CustomerException>> Handle(
        RedeemPointsCommand request, CancellationToken cancellationToken)
    {
        var customerId = new CustomerId(request.CustomerId);
        var rewardId = new RewardId(request.RewardId);

        var maybeCustomer = await customerQueries.GetById(customerId, cancellationToken);
        if (!maybeCustomer.HasValue)
            return new CustomerNotFoundException(customerId);

        var customer = maybeCustomer.ValueOr(default(Customer)!);

        var maybeReward = await rewardRepository.GetById(rewardId, cancellationToken);
        if (!maybeReward.HasValue)
            return new RedeemRewardNotFoundException(customerId, rewardId);

        var reward = maybeReward.ValueOr(default(Reward)!);

        if (!reward.IsActive)
            return new RedeemRewardInactiveException(customerId, rewardId);
        if (reward.StockQuantity == 0)
            return new RedeemRewardOutOfStockException(customerId, rewardId);
        if (customer.TotalPoints < reward.PointsCost)
            return new InsufficientPointsException(customerId, reward.PointsCost, customer.TotalPoints);

        try
        {
            var earned = await transactionRepository.GetActiveEarnedFor(customerId, cancellationToken);
            var remainingCost = reward.PointsCost;
            foreach (var tx in earned)
            {
                if (remainingCost == 0) break;
                var take = Math.Min(tx.Remaining, remainingCost);
                if (take > 0)
                {
                    tx.Consume(take);
                    remainingCost -= take;
                }
            }

            if (remainingCost > 0)
            {
                return new InsufficientPointsException(
                    customerId, reward.PointsCost, reward.PointsCost - remainingCost);
            }

            customer.Redeem(reward.PointsCost);
            reward.Decrement();

            var redeemTx = PointTransaction.Redeemed(
                PointTransactionId.New(),
                customer.Id,
                reward.PointsCost,
                $"Redeemed: {reward.Name}",
                DateTime.UtcNow);

            customerRepository.Update(customer);
            rewardRepository.Update(reward);
            await transactionRepository.Add(redeemTx, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return customer;
        }
        catch (Exception ex)
        {
            return new CustomerUnknownException(customerId, ex);
        }
    }
}
