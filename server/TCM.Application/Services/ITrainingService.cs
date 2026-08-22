using TCM.Application.Common;
using TCM.Application.Dtos.Trainings;
using TCM.Domain.Enums;

namespace TCM.Application.Services;

/// <summary>
/// Trainings, invitations and attendance (SPEC sections 6.5 and 6.6). Every method takes the
/// caller's identity from the token, never from the request body, and decides here — the
/// controller's role attribute is the second lock, not the only one.
/// </summary>
public interface ITrainingService
{
    /// <summary>The coach's table view (SPEC section 6.5), filtered by title, status and type.</summary>
    Task<ApiResponse<IReadOnlyList<TrainingDto>>> GetTrainingsAsync(
        string callerId, string? title, TrainingStatus? status, TrainingType? type, CancellationToken ct = default);

    /// <summary>The coach's calendar view (SPEC section 6.5).</summary>
    Task<ApiResponse<IReadOnlyList<TrainingDto>>> GetCalendarAsync(
        string callerId, int? year, int? month, CancellationToken ct = default);

    /// <summary>
    /// Training details (SPEC section 6.6). A coach sees any training in their own club; a
    /// member sees it only if they were invited to it.
    /// </summary>
    Task<ApiResponse<TrainingDetailsDto>> GetDetailsAsync(
        int trainingId, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>
    /// Creates the training, one Invited attendance row per invitee, and emails each of them a
    /// link to the details screen. A failed email never fails the training.
    /// </summary>
    Task<ApiResponse<TrainingDetailsDto>> CreateAsync(
        EditTrainingDto dto, string callerId, CancellationToken ct = default);

    /// <summary>
    /// Edits the training and reconciles the invitee list. An invitee who is dropped keeps their
    /// row if they have already reported attendance — removing it would erase history.
    /// </summary>
    Task<ApiResponse<TrainingDetailsDto>> UpdateAsync(
        int trainingId, EditTrainingDto dto, string callerId, CancellationToken ct = default);

    Task<ApiResponse<Unit>> DeleteAsync(int trainingId, string callerId, CancellationToken ct = default);

    /// <summary>
    /// Reports Present/Absent plus a reason (SPEC section 6.6). The coach may report for anyone
    /// invited to a training in their club; a member may report for themselves and no one else.
    /// </summary>
    Task<ApiResponse<TrainingAttendeeDto>> ReportAttendanceAsync(
        int trainingId, ReportAttendanceDto dto, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>
    /// The performance score (SPEC sections 5 and 6.6). Coach only — a member may not score
    /// anyone, including themselves.
    /// </summary>
    Task<ApiResponse<TrainingAttendeeDto>> SetPerformanceAsync(
        int trainingId, string memberId, SetPerformanceDto dto, string callerId, CancellationToken ct = default);

    /// <summary>
    /// The attendance and performance charts of SPEC section 6.4. A coach may read this for a
    /// member of their own club; a member only for themselves.
    /// </summary>
    Task<ApiResponse<MemberAttendanceSummaryDto>> GetMemberAttendanceAsync(
        string memberId, int? year, string callerId, bool isCoach, CancellationToken ct = default);
}
