namespace MLMConquerorGlobalEdition.SharedKernel;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string errorCode, string error) => new(false, default, errorCode, error);
}
