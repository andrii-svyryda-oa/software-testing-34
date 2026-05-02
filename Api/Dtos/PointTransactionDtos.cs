using Domain.PointTransactions;

namespace Api.Dtos;

public record PointTransactionDto(
    Guid Id,
    Guid CustomerId,
    int Points,
    PointTransactionType Type,
    string Description,
    DateTime CreatedAt)
{
    public static PointTransactionDto FromDomainModel(PointTransaction t) =>
        new(t.Id.Value, t.CustomerId.Value, t.Points, t.Type, t.Description, t.CreatedAt);
}
