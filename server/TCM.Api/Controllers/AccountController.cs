using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Services;
using TCM.Domain.Constants;

namespace TCM.Api.Controllers;

/// <summary>SPEC section 6.1 — login, coach-only registration, and password reset.</summary>
public class AccountController(IAccountService accountService) : BaseController
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<MemberTokenDto>>> Login(
        [FromBody] LoginMemberDto dto, CancellationToken ct)
        => HandleResult(await accountService.LoginAsync(dto, ct));

    /// <summary>
    /// Coach-only. SPEC section 6.1 is explicit that there is no public self-registration —
    /// a coach adding a member is the only way into the system.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<RegisteredMemberDto>>> Register(
        [FromBody] MemberRegisterDto dto, CancellationToken ct)
        => HandleResult(await accountService.RegisterAsync(dto, CallerId, ct));

    /// <summary>
    /// Always answers the same way whether or not the email is registered, so the endpoint
    /// cannot be used to enumerate the club's members.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<Unit>>> ForgotPassword(
        [FromBody] ForgotPasswordDto dto, CancellationToken ct)
        => HandleResult(await accountService.ForgotPasswordAsync(dto, ct));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<Unit>>> ResetPassword(
        [FromBody] ResetPasswordDto dto, CancellationToken ct)
        => HandleResult(await accountService.ResetPasswordAsync(dto, ct));
}
