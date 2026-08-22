using FluentValidation.Results;

namespace TCM.Application.Common;

/// <summary>
/// Turns a FluentValidation result into the failure envelope the API returns, so every slice
/// reports validation problems the same way.
/// </summary>
public static class ValidationExtensions
{
    public static ApiResponse<T> ToFailure<T>(this ValidationResult result) =>
        ApiResponse<T>.Fail(
            "Some of the details supplied are not valid.",
            ErrorKind.Validation,
            result.Errors.Select(e => e.ErrorMessage).ToList());
}
