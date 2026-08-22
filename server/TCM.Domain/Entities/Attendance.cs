using TCM.Domain.Enums;

namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: Attendances. One row per invited member per training. Created as
/// <see cref="AttendanceStatus.Invited"/> when the coach schedules the training, then updated
/// when attendance is reported (section 6.6).
/// </summary>
public class Attendance
{
    public int Id { get; set; }

    /// <summary>UTC.</summary>
    public DateTime Date { get; set; }

    /// <summary>Free text — in practice the reason for an absence, or a note on the session.</summary>
    public string? Description { get; set; }

    public int TrainingId { get; set; }
    public Training Training { get; set; } = null!;

    public required string MemberId { get; set; }
    public ApplicationUser Member { get; set; } = null!;

    /// <summary>
    /// Coach-entered performance score for this member at this training (section 6.6), feeding
    /// the performance line chart in section 6.4. Null until the coach scores it.
    /// </summary>
    public int? Performance { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Invited;
}
