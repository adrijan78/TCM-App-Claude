namespace TCM.Domain.Enums;

/// <summary>
/// SPEC section 4: Attendances.Status. A row is created as <see cref="Invited"/> when the
/// coach adds a member to a training (section 6.5); the member or the coach then reports
/// <see cref="Present"/> or <see cref="Absent"/> (section 6.6).
/// </summary>
public enum AttendanceStatus
{
    Invited = 0,
    Present = 1,
    Absent = 2
}
