using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Domain.Constants;

namespace TCM.Api.Controllers;

/// <summary>
/// Base for every controller in the API (SPEC section 3.1). Supplies the caller's identity from
/// the validated JWT and converts an <see cref="ApiResponse{T}"/> into the right HTTP status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// The caller's own user id, taken from the token. Never take an identity from the request
    /// body or route — that is the IDOR the whole authorization model exists to prevent.
    /// </summary>
    protected string CallerId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    protected bool IsCoach => User.IsInRole(Roles.Coach);

    protected ActionResult<ApiResponse<T>> HandleResult<T>(ApiResponse<T> response)
    {
        if (response.Success) return Ok(response);

        return response.ErrorKind switch
        {
            ErrorKind.NotFound     => NotFound(response),
            ErrorKind.Forbidden    => StatusCode(StatusCodes.Status403Forbidden, response),
            ErrorKind.Unauthorized => Unauthorized(response),
            ErrorKind.Conflict     => Conflict(response),
            ErrorKind.External     => StatusCode(StatusCodes.Status502BadGateway, response),
            _                      => BadRequest(response)
        };
    }
}
