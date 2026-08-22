using TCM.Application.Dtos.Members;
using TCM.Domain.Entities;

namespace TCM.Application.Abstractions;

/// <summary>
/// SPEC section 3.1 names MemberRepository explicitly. Everything here projects straight to a
/// DTO — an <see cref="ApplicationUser"/> carries the password hash and the Stripe customer id,
/// so the fewer places that hold one, the fewer places can leak one.
/// </summary>
public interface IMemberRepository : IRepository<ApplicationUser>
{
    /// <summary>
    /// The member list of SPEC section 6.3, narrowed by the caller's own club and by the
    /// screen's filters. Every filter is applied in SQL; <paramref name="today"/> is passed in
    /// so the age bands become a date-of-birth range the database can compare.
    /// </summary>
    Task<IReadOnlyList<MemberDto>> SearchAsync(
        int? clubId, MemberFilterDto filter, DateOnly today, CancellationToken ct = default);

    /// <summary>One member as a DTO, or null when no such row exists.</summary>
    Task<MemberDto?> GetMemberAsync(string memberId, DateOnly today, CancellationToken ct = default);

    /// <summary>Belt exam history, newest first (SPEC section 6.4).</summary>
    Task<IReadOnlyList<MemberBeltDto>> GetBeltHistoryAsync(string memberId, CancellationToken ct = default);

    /// <summary>A single belt exam with its belt loaded, or null. Tracked, so it can be removed.</summary>
    Task<MemberBelt?> GetBeltRecordAsync(int beltRecordId, CancellationToken ct = default);

    /// <summary>True when the belt lookup row exists — checked before a promotion is recorded.</summary>
    Task<bool> BeltExistsAsync(int beltId, CancellationToken ct = default);

    /// <summary>How many belt exams the member already has.</summary>
    Task<int> CountBeltsAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Clears the member's current-belt flag in a single UPDATE and commits it. Must run before
    /// a new current belt is inserted: the unique filtered index on <c>MemberBelts</c> allows
    /// only one flagged row per member, and SQL Server checks it per statement.
    /// </summary>
    Task ClearCurrentBeltAsync(string memberId, CancellationToken ct = default);

    /// <summary>Adds a belt exam and commits it. The caller decides the current-belt flag.</summary>
    Task<MemberBelt> AddBeltAsync(MemberBelt belt, CancellationToken ct = default);

    /// <summary>Removes a belt exam and commits it.</summary>
    Task RemoveBeltAsync(MemberBelt belt, CancellationToken ct = default);

    /// <summary>
    /// Flags the member's most recent remaining belt as current and commits, so deleting the
    /// current belt does not leave the member with none. Returns false when they have none left.
    /// </summary>
    Task<bool> PromoteLatestBeltToCurrentAsync(string memberId, CancellationToken ct = default);
}
