using TCM.Application.Common;
using TCM.Application.Dtos.Common;

namespace TCM.Application.Services;

public interface ICommonService
{
    Task<ApiResponse<IReadOnlyList<BeltDto>>> GetBeltsAsync(CancellationToken ct = default);

    /// <summary>
    /// Dashboard numbers (SPEC section 6.2). Scoped to the caller's own club, which is taken
    /// from their account rather than from the request.
    /// </summary>
    Task<ApiResponse<ClubNumbersInfoDto>> GetClubNumbersAsync(
        string callerId, int? year, int? month, CancellationToken ct = default);
}
