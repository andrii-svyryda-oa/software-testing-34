using Api.Dtos;
using Api.Modules.Errors;
using Application.Rewards.Commands;
using Application.Rewards.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/rewards")]
public class RewardsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RewardDto>>> List(CancellationToken cancellationToken)
    {
        var rewards = await sender.Send(new GetAvailableRewardsQuery(), cancellationToken);
        return Ok(rewards.Select(RewardDto.FromDomainModel).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<RewardDto>> Create(
        [FromBody] CreateRewardDto dto, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateRewardCommand
        {
            Name = dto.Name,
            Description = dto.Description,
            PointsCost = dto.PointsCost,
            Category = dto.Category,
            StockQuantity = dto.StockQuantity
        }, cancellationToken);

        return result.Match<ActionResult<RewardDto>>(
            r => CreatedAtAction(nameof(List), null, RewardDto.FromDomainModel(r)),
            e => e.ToObjectResult());
    }
}
