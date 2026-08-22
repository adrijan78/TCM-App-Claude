using TCM.Application.Dtos.Common;
using TCM.Domain.Enums;

namespace TCM.Application.Dtos.Members;

/// <summary>
/// SPEC section 3.1 — MemberDto. Serves both the coach's member list (section 6.3) and the
/// member profile header (section 6.4).
/// </summary>
/// <remarks>
/// Carries no credential or billing fields: <c>PasswordHash</c>, <c>SecurityStamp</c> and
/// <c>StripeCustomerId</c> never leave the server. <c>Age</c> is derived from
/// <see cref="DateOfBirth"/> at query time rather than stored.
/// </remarks>
public record MemberDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    DateOnly DateOfBirth,
    int Age,
    DateOnly StartedOn,
    bool IsActive,
    bool IsCoach,
    decimal? Height,
    decimal? Weight,
    BeltDto? CurrentBelt,
    Guid? PhotoPublicId);

/// <summary>
/// The member-list filters of SPEC section 6.3. All three are optional; each one narrows the
/// SQL query rather than the materialised list.
/// </summary>
public record MemberFilterDto(string? Search, int? BeltId, AgeGroup? AgeGroup);

/// <summary>
/// The "Edit Data" form of SPEC section 6.4.
/// </summary>
/// <remarks>
/// Deliberately has no <c>IsCoach</c>, <c>IsActive</c>, <c>Role</c> or <c>ClubId</c>. A member
/// editing their own profile must not be able to promote themselves, reactivate a closed
/// account or move club by adding a field to the request body, and the surest way to guarantee
/// that is for the shape they post into to have nowhere to put those values. Status is changed
/// only through the coach-only deactivate route; role and club are set at registration.
/// </remarks>
public record EditMemberDto(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    DateOnly DateOfBirth,
    decimal? Height,
    decimal? Weight);

/// <summary>
/// One belt exam from the member's history (SPEC sections 4 and 6.4). Wraps the shared
/// <see cref="BeltDto"/> rather than repeating the belt's own fields.
/// </summary>
public record MemberBeltDto(
    int Id,
    string MemberId,
    BeltDto Belt,
    DateOnly DateReceived,
    string? Description,
    bool IsCurrentBelt);

/// <summary>The coach-only "add belt exam" form (SPEC section 6.4).</summary>
public record AddMemberBeltDto(int BeltId, DateOnly DateReceived, string? Description, bool IsCurrentBelt);
