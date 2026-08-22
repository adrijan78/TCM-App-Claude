using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Application.Dtos.Members;
using TCM.Application.Services;
using TCM.Domain.Constants;
using TCM.Domain.Enums;

namespace TCM.Api.Controllers;

/// <summary>
/// The member list (SPEC section 6.3) and the member profile with its belt exams
/// (SPEC section 6.4).
/// </summary>
/// <remarks>
/// The role attributes here are the first half of the authorization rule; the second half — is
/// this the caller's own record, and is this coach's club the member's club — lives in
/// <see cref="IMemberService"/>, where a future controller cannot skip it. Nothing in this class
/// reads an identity from the route or the body.
/// </remarks>
[Authorize]
public class MembersController(IMemberService memberService) : BaseController
{
    /// <summary>The coach's member list with its three filters (SPEC section 6.3).</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MemberDto>>>> Get(
        [FromQuery] string? search,
        [FromQuery] int? beltId,
        [FromQuery] AgeGroup? ageGroup,
        CancellationToken ct)
        => HandleResult(await memberService.GetMembersAsync(
            new MemberFilterDto(search, beltId, ageGroup), CallerId, IsCoach, ct));

    /// <summary>A coach may read anyone in their own club; a member only themselves.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> GetById(string id, CancellationToken ct)
        => HandleResult(await memberService.GetMemberAsync(id, CallerId, IsCoach, ct));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> Update(
        string id, [FromBody] EditMemberDto dto, CancellationToken ct)
        => HandleResult(await memberService.UpdateMemberAsync(id, dto, CallerId, IsCoach, ct));

    /// <summary>
    /// PATCH rather than DELETE, and named for what it does: members are deactivated, never
    /// deleted, because their attendance, payment and note history references the row.
    /// </summary>
    [HttpPatch("{id}/deactivate")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<MemberDto>>> Deactivate(string id, CancellationToken ct)
        => HandleResult(await memberService.DeactivateAsync(id, CallerId, IsCoach, ct));

    [HttpGet("{id}/belts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MemberBeltDto>>>> GetBelts(
        string id, CancellationToken ct)
        => HandleResult(await memberService.GetBeltsAsync(id, CallerId, IsCoach, ct));

    [HttpPost("{id}/belts")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<MemberBeltDto>>> AddBelt(
        string id, [FromBody] AddMemberBeltDto dto, CancellationToken ct)
        => HandleResult(await memberService.AddBeltAsync(id, dto, CallerId, IsCoach, ct));

    [HttpDelete("{id}/belts/{beltRecordId:int}")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<Unit>>> DeleteBelt(
        string id, int beltRecordId, CancellationToken ct)
        => HandleResult(await memberService.DeleteBeltAsync(id, beltRecordId, CallerId, IsCoach, ct));
}
