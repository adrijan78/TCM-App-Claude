using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Domain.Constants;

namespace TCM.Api.Controllers;

/// <summary>
/// The role list that populates the "Role" dropdown on the coach's registration form
/// (SPEC section 6.1). Coach-only — a member has no reason to enumerate roles.
/// </summary>
[Authorize(Roles = Roles.Coach)]
public class RolesController(RoleManager<IdentityRole> roleManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> Get(CancellationToken ct)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name!))
            .ToListAsync(ct);

        return HandleResult(ApiResponse<IReadOnlyList<RoleDto>>.Ok(roles));
    }
}
