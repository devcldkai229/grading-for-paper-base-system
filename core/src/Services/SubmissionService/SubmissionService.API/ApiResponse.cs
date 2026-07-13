namespace SubmissionService.API;

public class ApiResponse<T>
{
    public int StatusCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public T Data { get; set; } = default!;

    public DateTime ResponsedAt { get; set; }
}
