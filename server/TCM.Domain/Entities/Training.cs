using TCM.Domain.Enums;

namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: Trainings. Created by a coach (section 6.5). Invited members appear as
/// <see cref="Attendance"/> rows.
/// </summary>
public class Training
{
    public int Id { get; set; }

    /// <summary>UTC. DateTime rather than DateTimeOffset so EF can translate Year/Month
    /// grouping for the dashboard chart; the club runs in a single time zone.</summary>
    public DateTime Date { get; set; }

    /// <summary>Doubles as the training's title in the table and calendar views (section 6.5).</summary>
    public required string Description { get; set; }

    /// <summary>The coach who created the training — SPEC section 4 calls this MemberId.</summary>
    public required string MemberId { get; set; }
    public ApplicationUser Member { get; set; } = null!;

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public TrainingType TrainingType { get; set; }
    public TrainingStatus Status { get; set; } = TrainingStatus.Active;

    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<Note> Notes { get; set; } = [];
}
