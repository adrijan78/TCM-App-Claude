namespace TCM.Application.Common;

/// <summary>
/// Why a service call failed. The API layer maps this to an HTTP status code, which keeps
/// services free of any dependency on ASP.NET Core while still producing correct responses.
/// </summary>
public enum ErrorKind
{
    None = 0,
    Validation,
    NotFound,
    Forbidden,
    Unauthorized,
    Conflict,
    External
}
