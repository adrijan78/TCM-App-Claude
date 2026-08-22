using TCM.Domain.Enums;

namespace TCM.Application.Dtos.Trainings;

/// <summary>
/// SPEC section 3.1 — TrainingDto. One row of the coach's table view and one entry of the
/// calendar feed (section 6.5): <c>Description</c> doubles as the title, and the two counts
/// drive the calendar's "members with attendance %" summary.
/// </summary>
public record TrainingDto(
    int Id,
    DateTime Date,
    string Description,
    TrainingType TrainingType,
    TrainingStatus Status,
    int InvitedCount,
    int PresentCount);

/// <summary>
/// SPEC section 3.1 — EditTrainingDto. The add/edit form of section 6.5, used for both create
/// and update: <see cref="MemberIds"/> is the complete invitee list, not a delta.
/// </summary>
public record EditTrainingDto(
    string Description,
    DateTime Date,
    TrainingType TrainingType,
    TrainingStatus Status,
    IReadOnlyList<string> MemberIds);

/// <summary>
/// SPEC section 3.1 — TrainingDetailsDto. The section 6.6 screen: the training itself plus the
/// invited members with what each of them reported.
/// </summary>
public record TrainingDetailsDto(
    int Id,
    DateTime Date,
    string Description,
    TrainingType TrainingType,
    TrainingStatus Status,
    IReadOnlyList<TrainingAttendeeDto> Attendees);

/// <summary>
/// One invited member on the section 6.6 screen. <see cref="AbsenceReason"/> is the
/// <c>Attendances.Description</c> column; <see cref="Performance"/> is coach-entered only.
/// </summary>
public record TrainingAttendeeDto(
    string MemberId,
    string FirstName,
    string LastName,
    AttendanceStatus Status,
    string? AbsenceReason,
    int? Performance);

/// <summary>
/// SPEC section 3.1 — MemberTrainingDto. One line of the "trainings held" list on a member's
/// profile (section 6.4), and one point of the performance line chart.
/// </summary>
public record MemberTrainingDto(
    int TrainingId,
    DateTime Date,
    string Description,
    TrainingType TrainingType,
    TrainingStatus TrainingStatus,
    AttendanceStatus AttendanceStatus,
    string? AbsenceReason,
    int? Performance);

/// <summary>
/// Everything behind the three charts of SPEC section 6.4's "Attendance and Performance" tab,
/// computed in one round trip so the client does not have to aggregate.
/// </summary>
public record MemberAttendanceSummaryDto(
    string MemberId,
    int? Year,
    int InvitedCount,
    int PresentCount,
    int AbsentCount,
    double AttendancePercentage,
    IReadOnlyList<MonthlyAttendanceDto> PerMonth,
    IReadOnlyList<MemberTrainingDto> Trainings);

/// <summary>One bar of the attendance-per-month chart (SPEC section 6.4).</summary>
public record MonthlyAttendanceDto(int Year, int Month, int Invited, int Present, int Absent);

/// <summary>
/// Reporting attendance for a training (SPEC section 6.6). <see cref="MemberId"/> is optional:
/// left null the caller is reporting for themselves, which is the only thing a member may do.
/// </summary>
public record ReportAttendanceDto(string? MemberId, AttendanceStatus Status, string? AbsenceReason);

/// <summary>The coach's per-member score for a training (SPEC section 6.6). Coach-only.</summary>
public record SetPerformanceDto(int Performance);

/// <summary>
/// A club member who may be invited to a training. Carries the address the invitation email is
/// sent to, so the create path does not need a second round trip per invitee.
/// </summary>
public record TrainingInviteeDto(string MemberId, string FirstName, string LastName, string? Email);
