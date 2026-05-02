using Api.Dtos;
using Api.Modules.Errors;
using Application.Customers.Commands;
using Application.Customers.Queries;
using Application.PointTransactions.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var customer = await sender.Send(new GetCustomerByIdQuery(id), cancellationToken);
        return customer.Match<ActionResult<CustomerDto>>(
            c => CustomerDto.FromDomainModel(c),
            () => NotFound());
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Register(
        [FromBody] RegisterCustomerDto dto, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterCustomerCommand
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone
        }, cancellationToken);

        return result.Match<ActionResult<CustomerDto>>(
            c => CreatedAtAction(nameof(Get), new { id = c.Id.Value }, CustomerDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }

    [HttpPost("{id:guid}/earn")]
    public async Task<ActionResult<CustomerDto>> Earn(
        Guid id, [FromBody] EarnPointsDto dto, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new EarnPointsCommand
        {
            CustomerId = id,
            BasePoints = dto.BasePoints,
            Description = dto.Description ?? "Points earned"
        }, cancellationToken);

        return result.Match<ActionResult<CustomerDto>>(
            c => CustomerDto.FromDomainModel(c),
            e => e.ToObjectResult());
    }

    [HttpPost("{id:guid}/redeem")]
    public async Task<ActionResult<CustomerDto>> Redeem(
        Guid id, [FromBody] RedeemPointsDto dto, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RedeemPointsCommand
        {
            CustomerId = id,
            RewardId = dto.RewardId
        }, cancellationToken);

        return result.Match<ActionResult<CustomerDto>>(
            c => CustomerDto.FromDomainModel(c),
            e => e.ToObjectResult());
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<PaginatedData<PointTransactionDto>>> History(
        Guid id,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var clampedPageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (Math.Max(1, page) - 1) * clampedPageSize;

        var result = await sender.Send(
            new GetCustomerHistoryQuery(id, skip, clampedPageSize),
            cancellationToken);

        return result.Match<ActionResult<PaginatedData<PointTransactionDto>>>(
            p => new PaginatedData<PointTransactionDto>(
                p.Data.Select(PointTransactionDto.FromDomainModel).ToList(),
                p.Total),
            e => e.ToObjectResult());
    }

    [HttpGet("{id:guid}/tier")]
    public async Task<ActionResult<CustomerTierDto>> Tier(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCustomerTierQuery(id), cancellationToken);
        return result.Match<ActionResult<CustomerTierDto>>(
            t => CustomerTierDto.FromResult(t),
            e => e.ToObjectResult());
    }
}
