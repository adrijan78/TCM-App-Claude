using TCM.Application.Common;
using TCM.Application.Dtos.Common;

namespace TCM.Application.Services;

public interface IPhotoService
{
    /// <summary>
    /// Stores a photo for <paramref name="memberId"/> and points that member's profile at it,
    /// replacing and deleting any previous one. A coach may upload for anyone in their club;
    /// a member only for themselves.
    /// </summary>
    Task<ApiResponse<PhotoDto>> UploadForMemberAsync(
        string memberId, PhotoUploadDto upload, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>Serves the bytes, subject to the same ownership rules as any member-scoped data.</summary>
    Task<ApiResponse<PhotoContentDto>> GetContentAsync(
        Guid publicId, string callerId, bool isCoach, CancellationToken ct = default);

    Task<ApiResponse<Unit>> DeleteAsync(
        Guid publicId, string callerId, bool isCoach, CancellationToken ct = default);
}
