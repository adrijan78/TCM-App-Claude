using Microsoft.AspNetCore.Identity;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Common;
using TCM.Domain.Entities;

namespace TCM.Application.Services;

/// <summary>
/// Shared lookups and the dashboard's club-wide numbers (SPEC sections 3.1 and 6.2). This is the
/// reference slice: later slices copy its shape — validate, authorize, delegate, wrap.
/// </summary>
public class CommonService(
    ICommonRepository repository,
    UserManager<ApplicationUser> userManager) : ICommonService
{
    public async Task<ApiResponse<IReadOnlyList<BeltDto>>> GetBeltsAsync(CancellationToken ct = default) =>
        ApiResponse<IReadOnlyList<BeltDto>>.Ok(await repository.GetBeltsAsync(ct));

    public async Task<ApiResponse<ClubNumbersInfoDto>> GetClubNumbersAsync(
        string callerId, int? year, int? month, CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return ApiResponse<ClubNumbersInfoDto>.Fail("Month must be between 1 and 12.");
        }

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller is null)
        {
            return ApiResponse<ClubNumbersInfoDto>.Forbidden();
        }

        // The club comes from the caller's own account, never from a query parameter — otherwise
        // any member could read another club's numbers by changing a value in the URL.
        var numbers = await repository.GetClubNumbersAsync(caller.ClubId, year, month, ct);

        return ApiResponse<ClubNumbersInfoDto>.Ok(numbers);
    }
}
