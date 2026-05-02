namespace Application.Common.Interfaces;

/// <summary>
/// Persists all pending changes staged by repositories in a single round-trip.
/// Enables atomic multi-aggregate writes (customer + reward + transaction) within one handler.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
