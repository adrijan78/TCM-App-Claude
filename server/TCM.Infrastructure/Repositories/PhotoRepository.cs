using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
using TCM.Application.Dtos.Common;
using TCM.Domain.Entities;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure.Repositories;

public class PhotoRepository(ApplicationDbContext context) : Repository<Photo>(context), IPhotoRepository
{
    public async Task<PhotoDto?> GetMetadataAsync(Guid publicId, CancellationToken ct = default) =>
        await Context.Photos
            .AsNoTracking()
            .Where(p => p.PublicId == publicId)
            .Select(p => new PhotoDto(p.PublicId, p.FileName, p.ContentType, p.SizeBytes, p.CreatedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<Photo?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        await Context.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == publicId, ct);

    public async Task<PhotoOwner?> GetOwnerAsync(Guid publicId, CancellationToken ct = default) =>
        await Context.Photos
            .AsNoTracking()
            .Where(p => p.PublicId == publicId)
            .Select(p => new PhotoOwner(
                p.Id,
                p.MemberId,
                p.MemberId == null ? null : p.Member!.ClubId))
            .FirstOrDefaultAsync(ct);

    public async Task DetachReferencesAsync(int photoId, CancellationToken ct = default)
    {
        // The AspNetUsers.PhotoId and Clubs.ClubLogoId foreign keys are Restrict, so anything
        // still pointing at this row must be cleared before it can be removed.
        await Context.Users
            .Where(u => u.PhotoId == photoId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PhotoId, (int?)null), ct);

        await Context.Clubs
            .Where(c => c.ClubLogoId == photoId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClubLogoId, (int?)null), ct);
    }
}
