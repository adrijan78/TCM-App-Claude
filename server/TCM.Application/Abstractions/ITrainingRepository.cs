using TCM.Application.Dtos.Trainings;
using TCM.Domain.Entities;
using TCM.Domain.Enums;

namespace TCM.Application.Abstractions;

/// <summary>
/// SPEC section 3.1 names TrainingRepository explicitly. Every filter and aggregate here runs
/// in SQL — nothing is loaded and then narrowed in memory.
/// </summary>
public interface ITrainingRepository : IRepository<Training>
{
    /// <summary>
    /// The coach's table view (SPEC section 6.5). <paramref name="title"/> matches the training's
    /// Description, which is what the screen calls the title.
    /// </summary>
    Task<IReadOnlyList<TrainingDto>> GetForClubAsync(
        int clubId, string? title, TrainingStatus? status, TrainingType? type, CancellationToken ct = default);

    /// <summary>The calendar feed (SPEC section 6.5), optionally narrowed to a year and month.</summary>
    Task<IReadOnlyList<TrainingDto>> GetCalendarAsync(
        int clubId, int? year, int? month, CancellationToken ct = default);

    /// <summary>The section 6.6 screen. Read-only projection, invitees ordered by name.</summary>
    Task<TrainingDetailsDto?> GetDetailsAsync(int trainingId, CancellationToken ct = default);

    /// <summary>
    /// The training with its attendance rows tracked, for the edit and delete paths. Returns
    /// null when there is no such training.
    /// </summary>
    Task<Training?> GetWithAttendancesAsync(int trainingId, CancellationToken ct = default);

    /// <summary>
    /// Which club a training belongs to, without loading it. Null when it does not exist. The
    /// authorization check needs this before anything else is read.
    /// </summary>
    Task<int?> GetClubIdAsync(int trainingId, CancellationToken ct = default);

    /// <summary>True when an Attendance row exists for this member — i.e. they were invited.</summary>
    Task<bool> IsInvitedAsync(int trainingId, string memberId, CancellationToken ct = default);

    /// <summary>The member's own invitation, tracked so it can be updated in place.</summary>
    Task<Attendance?> GetAttendanceAsync(int trainingId, string memberId, CancellationToken ct = default);

    /// <summary>
    /// The subset of <paramref name="memberIds"/> that are active members of the given club.
    /// Anything the caller supplied that is not in the result does not belong on the invitee list.
    /// </summary>
    Task<IReadOnlyList<TrainingInviteeDto>> GetClubMembersAsync(
        int clubId, IReadOnlyCollection<string> memberIds, CancellationToken ct = default);

    /// <summary>
    /// The data behind SPEC section 6.4's attendance and performance charts for one member,
    /// optionally restricted to a year.
    /// </summary>
    Task<MemberAttendanceSummaryDto> GetMemberAttendanceAsync(
        string memberId, int? year, CancellationToken ct = default);
}
