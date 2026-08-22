namespace TCM.Domain.Entities;

/// <summary>SPEC section 4: Belts. A lookup table, seeded once in grading order.</summary>
public class Belt
{
    public int Id { get; set; }
    public required string BeltName { get; set; }

    /// <summary>
    /// Grading order, lowest first. Not in SPEC section 4, but the member list filters by
    /// belt and the profile shows belt progression, both of which need a defined order that
    /// alphabetical sorting of <see cref="BeltName"/> would not give.
    /// </summary>
    public int Rank { get; set; }

    public ICollection<MemberBelt> MemberBelts { get; set; } = [];
}
