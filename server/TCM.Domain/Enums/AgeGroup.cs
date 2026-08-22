namespace TCM.Domain.Enums;

/// <summary>
/// The competition age bands the member list filters by (SPEC section 6.3). Not persisted —
/// a member's band is derived from <c>DateOfBirth</c>, so it changes on its own every birthday
/// and a stored column would go stale.
/// </summary>
/// <remarks>
/// The bands follow World Taekwondo's competition divisions. SPEC section 6.3 lists the groups
/// as "Kids, Juniors, Cadets, Seniors, etc.", which is not age order — WT names 12–14 Cadet and
/// 15–17 Junior. The enum is ordered by age here so a dropdown built from it reads sensibly;
/// the labels themselves are unchanged from the spec.
/// </remarks>
public enum AgeGroup
{
    /// <summary>Under 12.</summary>
    Kids = 0,

    /// <summary>12 to 14 inclusive.</summary>
    Cadets = 1,

    /// <summary>15 to 17 inclusive.</summary>
    Juniors = 2,

    /// <summary>18 and over.</summary>
    Seniors = 3
}

/// <summary>
/// The age ranges behind <see cref="AgeGroup"/>. Kept next to the enum so the numbers and the
/// names cannot drift apart.
/// </summary>
public static class AgeGroups
{
    /// <summary>
    /// Inclusive age bounds for a band. <c>MaxAge</c> is null for <see cref="AgeGroup.Seniors"/>,
    /// which has no upper limit.
    /// </summary>
    public static (int MinAge, int? MaxAge) Bounds(AgeGroup group) => group switch
    {
        AgeGroup.Kids => (0, 11),
        AgeGroup.Cadets => (12, 14),
        AgeGroup.Juniors => (15, 17),
        AgeGroup.Seniors => (18, null),
        _ => (0, null)
    };

    /// <summary>Whole years completed on <paramref name="today"/>.</summary>
    public static int AgeOn(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age < 0 ? 0 : age;
    }
}
