namespace SubmissionService.Application;

public enum OperationStatus { Success, Invalid, Forbidden, NotFound, Conflict }

public sealed class ServiceResult<T>
{
    public required OperationStatus Status { get; init; }

    public T? Data { get; init; }

    public string? Error { get; init; }

    public static ServiceResult<T> Ok(T data) => new() { Status = OperationStatus.Success, Data = data };

    public static ServiceResult<T> Invalid(string error) => new() { Status = OperationStatus.Invalid, Error = error };

    public static ServiceResult<T> Forbidden() => new() { Status = OperationStatus.Forbidden };

    public static ServiceResult<T> NotFound() => new() { Status = OperationStatus.NotFound };

    public static ServiceResult<T> Conflict(string error) => new() { Status = OperationStatus.Conflict, Error = error };
}
