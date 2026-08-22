namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: Photos. Member photos and the club logo.
/// </summary>
/// <remarks>
/// Decided 2026-08-22: the bytes live in SQL Server rather than Firebase Storage, which
/// supersedes SPEC section 2's file-storage choice and removes that dependency entirely.
/// SPEC section 4's <c>Url</c> column is gone — there is no external URL any more; the client
/// fetches the image from the API by <see cref="PublicId"/>. <c>PublicId</c> is kept, now as a
/// GUID, so photo addresses cannot be walked by incrementing a primary key.
/// </remarks>
public class Photo
{
    public int Id { get; set; }

    /// <summary>Opaque identifier used in the API route. Not the primary key, deliberately.</summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>Original name, kept for display only. Never used to build a path.</summary>
    public required string FileName { get; set; }

    /// <summary>Determined by sniffing the bytes, not by trusting the upload's declared type.</summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// The image itself, <c>varbinary(max)</c>. Never project this into a list query — loading
    /// a hundred members must not drag a hundred images into memory.
    /// </summary>
    public required byte[] Content { get; set; }

    public int SizeBytes { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Null for a club logo, which belongs to a <see cref="Club"/> rather than a member.</summary>
    public string? MemberId { get; set; }
    public ApplicationUser? Member { get; set; }
}
