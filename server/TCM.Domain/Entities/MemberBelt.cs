namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: MemberBelts. A member accumulates belts over time; exactly one of them is
/// flagged <see cref="IsCurrentBelt"/>. Enforcing that invariant is the service layer's job.
/// </summary>
public class MemberBelt
{
    public int Id { get; set; }

    public required string MemberId { get; set; }
    public ApplicationUser Member { get; set; } = null!;

    public int BeltId { get; set; }
    public Belt Belt { get; set; } = null!;

    public DateOnly DateReceived { get; set; }
    public string? Description { get; set; }
    public bool IsCurrentBelt { get; set; }
}
