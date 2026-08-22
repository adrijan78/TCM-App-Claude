using TCM.Application.Common;
using TCM.Application.Dtos.Account;

namespace TCM.Application.Services;

public interface IAccountService
{
    Task<ApiResponse<MemberTokenDto>> LoginAsync(LoginMemberDto dto, CancellationToken ct = default);

    /// <summary>Coach-only (SPEC section 6.1). <paramref name="callerId"/> is the registering coach.</summary>
    Task<ApiResponse<MemberTokenDto>> RegisterAsync(MemberRegisterDto dto, string callerId, CancellationToken ct = default);

    Task<ApiResponse<Unit>> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default);

    Task<ApiResponse<Unit>> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
}
