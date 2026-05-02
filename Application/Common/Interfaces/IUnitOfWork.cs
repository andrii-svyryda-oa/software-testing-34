namespace Application.Common.Interfaces;

/// <summary>
/// Persists all pending changes staged by repositories in a single round-trip.
/// Enables atomic multi-aggregate writes (customer + reward + transaction) within one handler.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Discards any tracked changes (e.g. after a concurrency conflict) so the next
    /// repository load returns fresh state.
    /// </summary>
    Task DiscardTrackedChangesAsync(CancellationToken cancellationToken);
}
