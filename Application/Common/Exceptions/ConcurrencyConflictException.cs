namespace Application.Common.Exceptions;

/// <summary>
/// Persistence-neutral signal that an optimistic-concurrency token mismatch occurred while
/// saving changes. Infrastructure-layer code translates EF Core's
/// <c>DbUpdateConcurrencyException</c> into this so the Application layer remains
/// persistence-agnostic.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException) { }
}
