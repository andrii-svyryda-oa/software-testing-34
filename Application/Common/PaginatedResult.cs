namespace Application.Common;

public record PaginatedResult<T>(IReadOnlyList<T> Data, int Total);
