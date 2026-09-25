namespace Memoressa.Application.Common;

public class ServiceResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; } = 400;

    public static ServiceResult Ok() => new() { Success = true, StatusCode = 200 };

    public static ServiceResult Fail(string error, int statusCode = 400) =>
        new() { Success = false, Error = error, StatusCode = statusCode };

    public static ServiceResult NotFound(string error = "Not found") =>
        Fail(error, 404);

    public static ServiceResult Forbidden(string error = "Forbidden") =>
        Fail(error, 403);
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Ok(T data) =>
        new() { Success = true, Data = data, StatusCode = 200 };

    public static ServiceResult<T> Created(T data) =>
        new() { Success = true, Data = data, StatusCode = 201 };

    public new static ServiceResult<T> Fail(string error, int statusCode = 400) =>
        new() { Success = false, Error = error, StatusCode = statusCode };

    public new static ServiceResult<T> NotFound(string error = "Not found") =>
        Fail(error, 404);
}
