namespace Api.Dtos;

public record PaginatedData<T>(IReadOnlyList<T> Data, int Total);
