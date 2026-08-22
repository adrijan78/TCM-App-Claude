using Microsoft.AspNetCore.Identity;

namespace TCM.Domain.Entities;

/// <summary>
/// The AspNetUsers table from SPEC section 4 — a standard ASP.NET Identity user extended with
/// the club's domain fields. Both coaches and members are rows here; <see cref="IsCoach"/> and
/// the assigned role distinguish them.
/// </summary>
/// <remarks>
/// SPEC section 4 also lists a <c>PasswordSalt</c> column. ASP.NET Identity's
/// <c>PasswordHasher</c> embeds a per-password salt inside <c>PasswordHash</c> itself, so a
/// separate salt column would always be empty. Storing one would only make sense with a
/// hand-rolled hasher, which SPEC section 7 rules out. The column is therefore not implemented.
/// </remarks>
public class ApplicationUser : IdentityUser
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    /// <summary>Join date — surfaced as "join date" in the member list (section 6.3).</summary>
    public DateOnly StartedOn { get; set; }

    /// <summary>
    /// Members are deactivated, never deleted (section 6.3): attendance, payment and note
    /// history all reference this row.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public bool IsCoach { get; set; }

    /// <summary>Centimetres.</summary>
    public decimal? Height { get; set; }

    /// <summary>Kilograms.</summary>
    public decimal? Weight { get; set; }

    public int? ClubId { get; set; }
    public Club? Club { get; set; }

    /// <summary>Profile photo. Nullable — a member may have none.</summary>
    public int? PhotoId { get; set; }
    public Photo? Photo { get; set; }

    /// <summary>Set when the coach registers the member (section 6.1). Never returned to a client.</summary>
    public string? StripeCustomerId { get; set; }

    public ICollection<MemberBelt> Belts { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Attendance> Attendances { get; set; } = [];
}
