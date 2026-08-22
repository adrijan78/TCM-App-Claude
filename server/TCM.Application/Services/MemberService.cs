using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Common;
using TCM.Application.Dtos.Members;
using TCM.Domain.Entities;

namespace TCM.Application.Services;

/// <summary>
/// The member list, member profile and belt exams (SPEC sections 6.3 and 6.4).
/// </summary>
/// <remarks>
/// Two rules run through every method here. First, a member reaches their own row and nothing
/// else — the comparison is always against the caller id taken from the token, never against a
/// value from the route or the body. Second, a coach reaches only their own club. The role
/// attribute on the controller cannot express either, so neither may be left to it.
/// </remarks>
public class MemberService(
    IMemberRepository members,
    UserManager<ApplicationUser> userManager,
    IValidator<EditMemberDto> editValidator,
    IValidator<AddMemberBeltDto> beltValidator,
    ILogger<MemberService> logger) : IMemberService
{
    /// <summary>Long enough for any real name or address, short enough not to be a payload.</summary>
    private const int MaxSearchLength = 100;

    public async Task<ApiResponse<IReadOnlyList<MemberDto>>> GetMembersAsync(
        MemberFilterDto filter, string callerId, CancellationToken ct = default)
    {
        if (filter.Search is { Length: > MaxSearchLength })
        {
            return ApiResponse<IReadOnlyList<MemberDto>>.Fail(
                $"The search text must be {MaxSearchLength} characters or fewer.");
        }

        if (filter.BeltId is <= 0)
        {
            return ApiResponse<IReadOnlyList<MemberDto>>.Fail("The belt filter is not a valid belt.");
        }

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller is null)
        {
            return ApiResponse<IReadOnlyList<MemberDto>>.Forbidden();
        }

        // The club comes from the coach's own account. There is no clubId parameter on this
        // route precisely so that there is nothing to tamper with (SPEC section 9: 1 coach : 1 club).
        var list = await members.SearchAsync(caller.ClubId, filter, Today, ct);

        return ApiResponse<IReadOnlyList<MemberDto>>.Ok(list);
    }

    public async Task<ApiResponse<MemberDto>> GetMemberAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var (access, _) = await ResolveAsync(memberId, callerId, isCoach, ct);
        if (access is not Access.Allowed)
        {
            return Refuse<MemberDto>(access);
        }

        var member = await members.GetMemberAsync(memberId, Today, ct);
        return member is null
            ? ApiResponse<MemberDto>.NotFound("Member not found.")
            : ApiResponse<MemberDto>.Ok(member);
    }

    public async Task<ApiResponse<MemberDto>> UpdateMemberAsync(
        string memberId, EditMemberDto dto, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var validation = await editValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<MemberDto>();
        }

        var (access, member) = await ResolveAsync(memberId, callerId, isCoach, ct);
        if (access is not Access.Allowed || member is null)
        {
            return Refuse<MemberDto>(access);
        }

        var email = dto.Email.Trim();
        if (!string.Equals(member.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await userManager.FindByEmailAsync(email);
            if (clash is not null && clash.Id != member.Id)
            {
                return ApiResponse<MemberDto>.Conflict("A member with that email already exists.");
            }

            // The email is also the sign-in name, so the two have to move together. UpdateAsync
            // re-normalises both and runs Identity's own uniqueness validators over them.
            member.Email = email;
            member.UserName = email;
        }

        member.FirstName = dto.FirstName.Trim();
        member.LastName = dto.LastName.Trim();
        member.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        member.DateOfBirth = dto.DateOfBirth;
        member.Height = dto.Height;
        member.Weight = dto.Weight;

        // Nothing above touches IsCoach, IsActive, ClubId or the member's roles. EditMemberDto
        // has no such fields, so an extra property in the request body is simply not bound.

        var updated = await userManager.UpdateAsync(member);
        if (!updated.Succeeded)
        {
            return ApiResponse<MemberDto>.Fail(
                "Could not save the changes.",
                ErrorKind.Validation,
                updated.Errors.Select(e => e.Description).ToList());
        }

        logger.LogInformation("Member {MemberId} was updated by {CallerId}.", member.Id, callerId);

        var result = await members.GetMemberAsync(member.Id, Today, ct);
        return result is null
            ? ApiResponse<MemberDto>.NotFound("Member not found.")
            : ApiResponse<MemberDto>.Ok(result, "Member updated.");
    }

    public async Task<ApiResponse<MemberDto>> DeactivateAsync(
        string memberId, string callerId, CancellationToken ct = default)
    {
        // Checked before anything is loaded: a coach who deactivates themselves cannot sign back
        // in to undo it, and there is no other coach in a 1 coach : 1 club model to do it for them.
        if (string.Equals(memberId, callerId, StringComparison.Ordinal))
        {
            return ApiResponse<MemberDto>.Fail("You cannot deactivate your own account.");
        }

        var (access, member) = await ResolveAsync(memberId, callerId, isCoach: true, ct);
        if (access is not Access.Allowed || member is null)
        {
            return Refuse<MemberDto>(access);
        }

        if (member.IsActive)
        {
            // Deactivated, never deleted (SPEC section 6.3): attendance, payments, belts and
            // notes all reference this row. Any token already issued stays valid until it
            // expires; the login path is what refuses an inactive account.
            member.IsActive = false;

            var updated = await userManager.UpdateAsync(member);
            if (!updated.Succeeded)
            {
                return ApiResponse<MemberDto>.Fail(
                    "Could not deactivate the member.",
                    ErrorKind.Validation,
                    updated.Errors.Select(e => e.Description).ToList());
            }

            logger.LogInformation("Member {MemberId} was deactivated by coach {CallerId}.", member.Id, callerId);
        }

        var result = await members.GetMemberAsync(member.Id, Today, ct);
        return result is null
            ? ApiResponse<MemberDto>.NotFound("Member not found.")
            : ApiResponse<MemberDto>.Ok(result, "Member deactivated.");
    }

    public async Task<ApiResponse<IReadOnlyList<MemberBeltDto>>> GetBeltsAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var (access, _) = await ResolveAsync(memberId, callerId, isCoach, ct);
        if (access is not Access.Allowed)
        {
            return Refuse<IReadOnlyList<MemberBeltDto>>(access);
        }

        var history = await members.GetBeltHistoryAsync(memberId, ct);
        return ApiResponse<IReadOnlyList<MemberBeltDto>>.Ok(history);
    }

    public async Task<ApiResponse<MemberBeltDto>> AddBeltAsync(
        string memberId, AddMemberBeltDto dto, string callerId, CancellationToken ct = default)
    {
        var validation = await beltValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<MemberBeltDto>();
        }

        var (access, member) = await ResolveAsync(memberId, callerId, isCoach: true, ct);
        if (access is not Access.Allowed || member is null)
        {
            return Refuse<MemberBeltDto>(access);
        }

        if (!await members.BeltExistsAsync(dto.BeltId, ct))
        {
            return ApiResponse<MemberBeltDto>.Fail("That belt does not exist.");
        }

        // A member's first belt is their current one whatever the form said — otherwise the
        // profile would show a belt history with no belt in it.
        var isFirst = await members.CountBeltsAsync(member.Id, ct) == 0;
        var makeCurrent = dto.IsCurrentBelt || isFirst;

        if (makeCurrent)
        {
            // Clear first, insert second. Exactly one belt per member may carry the flag, and
            // the database enforces that with a unique filtered index it checks per statement.
            await members.ClearCurrentBeltAsync(member.Id, ct);
        }

        var belt = new MemberBelt
        {
            MemberId = member.Id,
            BeltId = dto.BeltId,
            DateReceived = dto.DateReceived,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsCurrentBelt = makeCurrent
        };

        var stored = await members.AddBeltAsync(belt, ct);

        logger.LogInformation(
            "Coach {CallerId} recorded belt exam {BeltRecordId} for member {MemberId}.",
            callerId, stored.Id, member.Id);

        return ApiResponse<MemberBeltDto>.Ok(new MemberBeltDto(
            stored.Id,
            stored.MemberId,
            new BeltDto(stored.Belt.Id, stored.Belt.BeltName, stored.Belt.Rank),
            stored.DateReceived,
            stored.Description,
            stored.IsCurrentBelt));
    }

    public async Task<ApiResponse<Unit>> DeleteBeltAsync(
        string memberId, int beltRecordId, string callerId, CancellationToken ct = default)
    {
        var (access, member) = await ResolveAsync(memberId, callerId, isCoach: true, ct);
        if (access is not Access.Allowed || member is null)
        {
            return Refuse<Unit>(access);
        }

        var record = await members.GetBeltRecordAsync(beltRecordId, ct);

        // The belt exam must belong to the member in the route. Without this, a coach could
        // delete any belt row in the database by pairing it with a member they may reach.
        if (record is null || record.MemberId != member.Id)
        {
            return ApiResponse.NotFound("Belt exam not found.");
        }

        var wasCurrent = record.IsCurrentBelt;
        await members.RemoveBeltAsync(record, ct);

        if (wasCurrent)
        {
            // Removing the current belt would otherwise leave the member with a history and no
            // current belt at all, which the profile and the member list both read.
            await members.PromoteLatestBeltToCurrentAsync(member.Id, ct);
        }

        logger.LogInformation(
            "Coach {CallerId} deleted belt exam {BeltRecordId} for member {MemberId}.",
            callerId, beltRecordId, member.Id);

        return ApiResponse.Ok("Belt exam deleted.");
    }

    /// <summary>UTC, matching how every date in this schema is stored.</summary>
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// The whole authorization decision for a member-scoped call, in one place so no method can
    /// forget half of it.
    /// </summary>
    private async Task<(Access Access, ApplicationUser? Member)> ResolveAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(callerId))
        {
            return (Access.Forbidden, null);
        }

        // Refused before the row is even loaded, so a member cannot use response timing or a
        // 404-versus-403 difference to learn which member ids exist.
        if (!isCoach && !string.Equals(memberId, callerId, StringComparison.Ordinal))
        {
            return (Access.Forbidden, null);
        }

        var member = await userManager.FindByIdAsync(memberId);
        if (member is null)
        {
            return (Access.NotFound, null);
        }

        if (isCoach && !await InSameClubAsync(callerId, member.ClubId))
        {
            return (Access.Forbidden, null);
        }

        return (Access.Allowed, member);
    }

    private async Task<bool> InSameClubAsync(string callerId, int? clubId)
    {
        var caller = await userManager.FindByIdAsync(callerId);
        return caller is not null && caller.ClubId is not null && caller.ClubId == clubId;
    }

    private static ApiResponse<T> Refuse<T>(Access access) => access switch
    {
        Access.NotFound => ApiResponse<T>.NotFound("Member not found."),
        _ => ApiResponse<T>.Forbidden()
    };

    private enum Access
    {
        Allowed,
        NotFound,
        Forbidden
    }
}
