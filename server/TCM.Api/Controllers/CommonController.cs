using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Application.Dtos.Common;
using TCM.Application.Services;

namespace TCM.Api.Controllers;

/// <summary>
/// Shared lookups and dashboard numbers (SPEC sections 3.1 and 6.2). Available to both roles —
/// a member sees their own club's figures on their home page.
/// </summary>
[Authorize]
public class CommonController(ICommonService commonService) : BaseController
{
    [HttpGet("belts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BeltDto>>>> GetBelts(CancellationToken ct)
        => HandleResult(await commonService.GetBeltsAsync(ct));

    [HttpGet("club-numbers")]
    public async Task<ActionResult<ApiResponse<ClubNumbersInfoDto>>> GetClubNumbers(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => HandleResult(await commonService.GetClubNumbersAsync(CallerId, year, month, ct));
}
