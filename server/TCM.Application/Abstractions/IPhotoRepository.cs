using TCM.Application.Dtos.Common;
using TCM.Domain.Entities;

namespace TCM.Application.Abstractions;

public interface IPhotoRepository : IRepository<Photo>
{
    /// <summary>Metadata only — never loads the <c>Content</c> column.</summary>
    Task<PhotoDto?> GetMetadataAsync(Guid publicId, CancellationToken ct = default);

    /// <summary>The full row, including bytes. Only for actually serving one image.</summary>
    Task<Photo?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);

    /// <summary>
    /// Who a photo belongs to, without loading it. Used for the ownership check before serving.
    /// Returns null when no such photo exists.
    /// </summary>
    Task<PhotoOwner?> GetOwnerAsync(Guid publicId, CancellationToken ct = default);

    /// <summary>Clears any user or club reference to this photo, so it can be deleted.</summary>
    Task DetachReferencesAsync(int photoId, CancellationToken ct = default);
}

/// <summary>
/// A photo's owning member and the club that member belongs to. A club logo has no member.
/// </summary>
public record PhotoOwner(int PhotoId, string? MemberId, int? ClubId);
