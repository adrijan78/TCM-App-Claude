using TCM.Domain.Enums;

namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: Notes. Written by one user about another (or about themselves). Two foreign
/// keys point at AspNetUsers, which is why both are configured with
/// <c>DeleteBehavior.Restrict</c> — SQL Server rejects multiple cascade paths to one table.
/// </summary>
public class Note
{
    public int Id { get; set; }

    public required string Title { get; set; }
    public required string Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Author.</summary>
    public required string FromMemberId { get; set; }
    public ApplicationUser FromMember { get; set; } = null!;

    /// <summary>Subject of the note — the member it appears on and who gets the email (section 6.8).</summary>
    public required string ToMemberId { get; set; }
    public ApplicationUser ToMember { get; set; } = null!;

    /// <summary>Set when the note was written against a specific training (section 6.6).</summary>
    public int? TrainingId { get; set; }
    public Training? Training { get; set; }

    /// <summary>High notes sort first in the member profile (section 6.8).</summary>
    public NotePriority Priority { get; set; } = NotePriority.Low;
}
