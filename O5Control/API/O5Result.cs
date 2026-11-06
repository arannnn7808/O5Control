namespace O5Control.API;

public class O5Result
{
    protected O5Result(bool success, string? errorCode, string? error)
    {
        Success = success;
        ErrorCode = errorCode;
        Error = error;
    }

    public bool Success { get; }

    public string? ErrorCode { get; }

    public string? Error { get; }

    public static O5Result Ok() => new(true, null, null);

    public static O5Result Fail(string code, string message) => new(false, code, message);

    public override string ToString() => Success ? "ok" : $"{ErrorCode}: {Error}";
}

public sealed class O5Result<T> : O5Result
{
    private O5Result(bool success, T? value, string? errorCode, string? error)
        : base(success, errorCode, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static O5Result<T> Ok(T value) => new(true, value, null, null);

    public static new O5Result<T> Fail(string code, string message) => new(false, default, code, message);
}

public static class O5ErrorCodes
{
    public const string NotConnected = "not_connected";
    public const string Timeout = "timeout";
    public const string Rejected = "rejected";
    public const string InvalidArgument = "invalid_argument";
    public const string Protected = "protected";
    public const string Internal = "internal";
}
