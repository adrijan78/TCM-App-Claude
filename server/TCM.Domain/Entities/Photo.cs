namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: Photos. Backed by Firebase Storage — <see cref="Url"/> is the public URL and
/// <see cref="PublicId"/> the storage object name used to delete it later.
/// </summary>
public class Photo
{
    public int Id { get; set; }
    public required string Url { get; set; }
    public required string PublicId { get; set; }

    /// <summary>Null for a club logo, which belongs to a <see cref="Club"/> rather than a member.</summary>
    public string? MemberId { get; set; }
    public ApplicationUser? Member { get; set; }
}
