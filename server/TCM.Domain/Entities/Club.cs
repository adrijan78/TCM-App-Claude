namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: Clubs. The model is deliberately 1 coach : 1 club for this version —
/// multi-club support is out of scope (sections 8 and 9).
/// </summary>
public class Club
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }

    public int? ClubLogoId { get; set; }
    public Photo? ClubLogo { get; set; }

    public ICollection<ApplicationUser> Members { get; set; } = [];
    public ICollection<Training> Trainings { get; set; } = [];
}
