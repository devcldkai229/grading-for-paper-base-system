namespace ExamCatalogService.API;

public class ApiResponse<T>
{
    public int StatusCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public T Data { get; set; } = default!;

    public DateTime ResponsedAt { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
