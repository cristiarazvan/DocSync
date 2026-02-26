namespace GoogDocsLite.Server.Application.Services;

public enum ServiceResultType
{
    Success = 0,
    NotFound = 1,
    Forbidden = 2,
    Locked = 3,
    ValidationError = 4,
    Conflict = 5
}

public sealed class ServiceResult
{
    public ServiceResultType Type { get; }
    public string? ErrorMessage { get; }

    public bool IsSuccess => Type == ServiceResultType.Success;

    private ServiceResult(ServiceResultType type, string? errorMessage = null)
    {
        Type = type;
        ErrorMessage = errorMessage;
    }

    public static ServiceResult Success() => new(ServiceResultType.Success);
    public static ServiceResult NotFound(string? message = null) => new(ServiceResultType.NotFound, message);
    public static ServiceResult Forbidden(string? message = null) => new(ServiceResultType.Forbidden, message);
    public static ServiceResult Locked(string? message = null) => new(ServiceResultType.Locked, message);
    public static ServiceResult ValidationError(string? message = null) => new(ServiceResultType.ValidationError, message);
    public static ServiceResult Conflict(string? message = null) => new(ServiceResultType.Conflict, message);
}

public sealed class ServiceResult<T>
{
    public ServiceResultType Type { get; }
    public string? ErrorMessage { get; }
    public T? Value { get; }

    public bool IsSuccess => Type == ServiceResultType.Success;

    private ServiceResult(ServiceResultType type, T? value = default, string? errorMessage = null)
    {
        Type = type;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public static ServiceResult<T> Success(T value) => new(ServiceResultType.Success, value);
    public static ServiceResult<T> NotFound(string? message = null) => new(ServiceResultType.NotFound, default, message);
    public static ServiceResult<T> Forbidden(string? message = null) => new(ServiceResultType.Forbidden, default, message);
    public static ServiceResult<T> Locked(string? message = null) => new(ServiceResultType.Locked, default, message);
    public static ServiceResult<T> ValidationError(string? message = null) => new(ServiceResultType.ValidationError, default, message);
    public static ServiceResult<T> Conflict(string? message = null) => new(ServiceResultType.Conflict, default, message);
}
