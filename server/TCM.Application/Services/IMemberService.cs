using TCM.Application.Common;
using TCM.Application.Dtos.Members;

namespace TCM.Application.Services;

/// <summary>
/// The member list, member profile and belt exam history (SPEC sections 6.3 and 6.4).
/// </summary>
/// <remarks>
/// Every method takes the caller's id from the validated token. Coach-only methods take it too:
/// the role attribute says "a coach", this layer says "a coach of this member's club".
/// </remarks>
public interface IMemberService
{
    /// <summary>The coach's member list, scoped to the coach's own club (SPEC section 6.3).</summary>
    Task<ApiResponse<IReadOnlyList<MemberDto>>> GetMembersAsync(
        MemberFilterDto filter, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>One member. A coach may read anyone in their club; a member only themselves.</summary>
    Task<ApiResponse<MemberDto>> GetMemberAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>The "Edit Data" form of SPEC section 6.4, under the same ownership rule.</summary>
    Task<ApiResponse<MemberDto>> UpdateMemberAsync(
        string memberId, EditMemberDto dto, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>
    /// Coach-only. Sets <c>IsActive</c> to false; the row itself is never deleted, because
    /// attendance, payment and note history all depend on it (SPEC section 6.3).
    /// </summary>
    Task<ApiResponse<MemberDto>> DeactivateAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>Belt exam history. Same ownership rule as the profile.</summary>
    Task<ApiResponse<IReadOnlyList<MemberBeltDto>>> GetBeltsAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>
    /// Coach-only. Records a belt exam, keeping the invariant that exactly one of a member's
    /// belts is flagged current.
    /// </summary>
    Task<ApiResponse<MemberBeltDto>> AddBeltAsync(
        string memberId, AddMemberBeltDto dto, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>Coach-only. Removes a belt exam, then re-flags the latest remaining one.</summary>
    Task<ApiResponse<Unit>> DeleteBeltAsync(
        string memberId, int beltRecordId, string callerId, bool isCoach, CancellationToken ct = default);
}
