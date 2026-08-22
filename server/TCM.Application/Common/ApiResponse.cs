namespace TCM.Application.Common;

/// <summary>
/// The envelope every controller action returns (SPEC section 3.1). Services return this
/// directly; <c>BaseController.HandleResult</c> turns it into the right HTTP status code.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Not serialised — the API layer uses it to choose a status code.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ErrorKind ErrorKind { get; init; } = ErrorKind.None;

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, ErrorKind kind = ErrorKind.Validation, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Message = message, ErrorKind = kind, Errors = errors ?? [] };

    public static ApiResponse<T> NotFound(string message = "Not found.") =>
        Fail(message, ErrorKind.NotFound);

    /// <summary>
    /// Use for both "wrong role" and "not your data". The message is deliberately identical in
    /// both cases so a caller cannot probe for the existence of another member's records.
    /// </summary>
    public static ApiResponse<T> Forbidden(string message = "You are not permitted to perform this action.") =>
        Fail(message, ErrorKind.Forbidden);

    public static ApiResponse<T> Conflict(string message) => Fail(message, ErrorKind.Conflict);
}

/// <summary>Convenience for actions that return no payload.</summary>
public static class ApiResponse
{
    public static ApiResponse<Unit> Ok(string? message = null) => ApiResponse<Unit>.Ok(Unit.Value, message);
    public static ApiResponse<Unit> Fail(string message, ErrorKind kind = ErrorKind.Validation) => ApiResponse<Unit>.Fail(message, kind);
    public static ApiResponse<Unit> NotFound(string message = "Not found.") => ApiResponse<Unit>.NotFound(message);
    public static ApiResponse<Unit> Forbidden(string message = "You are not permitted to perform this action.") => ApiResponse<Unit>.Forbidden(message);
}

/// <summary>Stand-in for "no payload" so <see cref="ApiResponse{T}"/> stays generic everywhere.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
