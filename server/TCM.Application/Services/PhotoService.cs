using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Common;
using TCM.Application.Options;
using TCM.Domain.Entities;

namespace TCM.Application.Services;

/// <summary>
/// Member and club photos, stored as bytes in the database (decided 2026-08-22, superseding
/// SPEC section 2's Firebase Storage choice).
/// </summary>
public class PhotoService(
    IPhotoRepository photos,
    UserManager<ApplicationUser> userManager,
    IOptions<PhotoSettings> settings,
    ILogger<PhotoService> logger) : IPhotoService
{
    public async Task<ApiResponse<PhotoDto>> UploadForMemberAsync(
        string memberId, PhotoUploadDto upload, string callerId, bool isCoach, CancellationToken ct = default)
    {
        // A member may only ever change their own photo.
        if (!isCoach && callerId != memberId)
        {
            return ApiResponse<PhotoDto>.Forbidden();
        }

        var member = await userManager.FindByIdAsync(memberId);
        if (member is null)
        {
            return ApiResponse<PhotoDto>.NotFound("Member not found.");
        }

        if (isCoach && !await InSameClubAsync(callerId, member))
        {
            // Same message as "not permitted" elsewhere: a coach of another club learns nothing.
            return ApiResponse<PhotoDto>.Forbidden();
        }

        if (upload.Content.Length == 0)
        {
            return ApiResponse<PhotoDto>.Fail("The uploaded file is empty.");
        }

        if (upload.Content.Length > settings.Value.MaxSizeBytes)
        {
            return ApiResponse<PhotoDto>.Fail(
                $"Images must be {settings.Value.MaxSizeBytes / 1024 / 1024} MB or smaller.");
        }

        // The real type comes from the bytes. What the client called it is irrelevant.
        var contentType = ImageFormatDetector.Detect(upload.Content);
        if (contentType is null)
        {
            return ApiResponse<PhotoDto>.Fail("That file is not a supported image (JPEG, PNG, GIF or WebP).");
        }

        var previousPhotoId = member.PhotoId;

        var photo = new Photo
        {
            FileName = SafeFileName(upload.FileName),
            ContentType = contentType,
            Content = upload.Content,
            SizeBytes = upload.Content.Length,
            CreatedAt = DateTime.UtcNow,
            MemberId = member.Id
        };

        await photos.AddAsync(photo, ct);
        await photos.SaveChangesAsync(ct);

        member.PhotoId = photo.Id;
        await userManager.UpdateAsync(member);

        // Replace, do not accumulate: the old image would otherwise sit in the table forever.
        if (previousPhotoId is not null)
        {
            await RemoveAsync(previousPhotoId.Value, ct);
        }

        logger.LogInformation("Stored photo {PublicId} for member {MemberId}.", photo.PublicId, member.Id);

        return ApiResponse<PhotoDto>.Ok(
            new PhotoDto(photo.PublicId, photo.FileName, photo.ContentType, photo.SizeBytes, photo.CreatedAt));
    }

    public async Task<ApiResponse<PhotoContentDto>> GetContentAsync(
        Guid publicId, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var owner = await photos.GetOwnerAsync(publicId, ct);
        if (owner is null)
        {
            return ApiResponse<PhotoContentDto>.NotFound("Photo not found.");
        }

        if (!await CanViewAsync(owner, callerId, isCoach))
        {
            // Deliberately the same shape of answer a stranger's photo id would get. These are
            // photographs of club members, some of them minors.
            return ApiResponse<PhotoContentDto>.Forbidden();
        }

        var photo = await photos.GetByPublicIdAsync(publicId, ct);
        if (photo is null)
        {
            return ApiResponse<PhotoContentDto>.NotFound("Photo not found.");
        }

        // Stable per stored image, so a repeat view is a 304 rather than another megabyte.
        var etag = $"\"{photo.PublicId:N}\"";

        return ApiResponse<PhotoContentDto>.Ok(
            new PhotoContentDto(photo.Content, photo.ContentType, photo.FileName, etag));
    }

    public async Task<ApiResponse<Unit>> DeleteAsync(
        Guid publicId, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var owner = await photos.GetOwnerAsync(publicId, ct);
        if (owner is null)
        {
            return ApiResponse.NotFound("Photo not found.");
        }

        if (!isCoach && owner.MemberId != callerId)
        {
            return ApiResponse.Forbidden();
        }

        if (isCoach && owner.MemberId is not null && !await InSameClubAsync(callerId, owner.ClubId))
        {
            return ApiResponse.Forbidden();
        }

        await RemoveAsync(owner.PhotoId, ct);
        return ApiResponse.Ok("Photo deleted.");
    }

    private async Task RemoveAsync(int photoId, CancellationToken ct)
    {
        await photos.DetachReferencesAsync(photoId, ct);

        var entity = await photos.GetByIdAsync(photoId, ct);
        if (entity is null) return;

        photos.Remove(entity);
        await photos.SaveChangesAsync(ct);
    }

    private async Task<bool> CanViewAsync(PhotoOwner owner, string callerId, bool isCoach)
    {
        // A club logo is visible to anyone signed in.
        if (owner.MemberId is null) return true;

        if (owner.MemberId == callerId) return true;

        return isCoach && await InSameClubAsync(callerId, owner.ClubId);
    }

    private async Task<bool> InSameClubAsync(string callerId, ApplicationUser member) =>
        await InSameClubAsync(callerId, member.ClubId);

    private async Task<bool> InSameClubAsync(string callerId, int? clubId)
    {
        var caller = await userManager.FindByIdAsync(callerId);
        return caller is not null && caller.ClubId is not null && caller.ClubId == clubId;
    }

    /// <summary>
    /// Keeps a display name only. Any directory component is discarded — the value is never used
    /// to build a path, and this stops a traversal-looking string being stored and echoed back.
    /// </summary>
    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name)) return "photo";
        return name.Length > 260 ? name[..260] : name;
    }
}
