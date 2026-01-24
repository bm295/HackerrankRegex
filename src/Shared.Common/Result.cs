namespace Shared.Common;

public readonly record struct Result(bool IsSuccess, Error? Error)
{
    public static Result Ok() => new(true, null);

    public static Result Fail(Error error) => new(false, error);

    public static Result Fail(string code, string message, string? details = null) =>
        new(false, new Error(code, message, details));
}

public readonly record struct Result<T>(bool IsSuccess, T? Value, Error? Error)
{
    public static Result<T> Ok(T value) => new(true, value, null);

    public static Result<T> Fail(Error error) => new(false, default, error);

    public static Result<T> Fail(string code, string message, string? details = null) =>
        new(false, default, new Error(code, message, details));
}
