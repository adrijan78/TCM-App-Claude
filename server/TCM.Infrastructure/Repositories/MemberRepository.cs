using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
using TCM.Application.Dtos.Common;
using TCM.Application.Dtos.Members;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure.Repositories;

public class MemberRepository(ApplicationDbContext context)
    : Repository<ApplicationUser>(context), IMemberRepository
{
    public async Task<IReadOnlyList<MemberDto>> SearchAsync(
        int? clubId, MemberFilterDto filter, DateOnly today, CancellationToken ct = default)
    {
        var query = Context.Users.AsNoTracking().Where(u => u.ClubId == clubId);

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // Contains becomes LIKE '%term%'. Both SQL Server's default collation and SQLite's
            // LIKE are case-insensitive for ASCII, so no ToLower() is needed — and adding one
            // would stop the database using an index on these columns.
            query = query.Where(u =>
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                (u.FirstName + " " + u.LastName).Contains(search) ||
                (u.Email != null && u.Email.Contains(search)));
        }

        if (filter.BeltId is int beltId)
        {
            query = query.Where(u => u.Belts.Any(b => b.IsCurrentBelt && b.BeltId == beltId));
        }

        if (filter.AgeGroup is AgeGroup group)
        {
            // Age is derived, not stored, so the band is turned into a date-of-birth window
            // before it reaches SQL. Computing an age per row in the query would work on neither
            // provider and would defeat any index on DateOfBirth.
            var (minAge, maxAge) = AgeGroups.Bounds(group);

            var newestAllowed = today.AddYears(-minAge);
            query = query.Where(u => u.DateOfBirth <= newestAllowed);

            if (maxAge is int max)
            {
                // Strictly after: someone born on this date turns max + 1 today.
                var oldestAllowed = today.AddYears(-(max + 1));
                query = query.Where(u => u.DateOfBirth > oldestAllowed);
            }
        }

        // Every member regardless of status (SPEC section 6.3) — inactive ones are shown, not
        // filtered out, because the list is also how a coach sees who has lapsed.
        var rows = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(Projection)
            .ToListAsync(ct);

        return rows.Select(row => ToDto(row, today)).ToList();
    }

    public async Task<MemberDto?> GetMemberAsync(
        string memberId, DateOnly today, CancellationToken ct = default)
    {
        var row = await Context.Users
            .AsNoTracking()
            .Where(u => u.Id == memberId)
            .Select(Projection)
            .FirstOrDefaultAsync(ct);

        return row is null ? null : ToDto(row, today);
    }

    public async Task<IReadOnlyList<MemberBeltDto>> GetBeltHistoryAsync(
        string memberId, CancellationToken ct = default) =>
        await Context.MemberBelts
            .AsNoTracking()
            .Where(mb => mb.MemberId == memberId)
            .OrderByDescending(mb => mb.DateReceived).ThenByDescending(mb => mb.Id)
            .Select(mb => new MemberBeltDto(
                mb.Id,
                mb.MemberId,
                new BeltDto(mb.Belt.Id, mb.Belt.BeltName, mb.Belt.Rank),
                mb.DateReceived,
                mb.Description,
                mb.IsCurrentBelt))
            .ToListAsync(ct);

    /// <summary>Tracked on purpose: the caller removes or re-flags the row it gets back.</summary>
    public async Task<MemberBelt?> GetBeltRecordAsync(int beltRecordId, CancellationToken ct = default) =>
        await Context.MemberBelts
            .Include(mb => mb.Belt)
            .FirstOrDefaultAsync(mb => mb.Id == beltRecordId, ct);

    public async Task<bool> BeltExistsAsync(int beltId, CancellationToken ct = default) =>
        await Context.Belts.AsNoTracking().AnyAsync(b => b.Id == beltId, ct);

    public async Task<int> CountBeltsAsync(string memberId, CancellationToken ct = default) =>
        await Context.MemberBelts.AsNoTracking().CountAsync(mb => mb.MemberId == memberId, ct);

    public async Task ClearCurrentBeltAsync(string memberId, CancellationToken ct = default) =>
        await Context.MemberBelts
            .Where(mb => mb.MemberId == memberId && mb.IsCurrentBelt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(mb => mb.IsCurrentBelt, false), ct);

    public async Task<MemberBelt> AddBeltAsync(MemberBelt belt, CancellationToken ct = default)
    {
        Context.MemberBelts.Add(belt);
        await SaveChangesAsync(ct);

        // Reloaded through the read path so the caller gets the belt's name and rank without a
        // second round trip being its problem.
        await Context.Entry(belt).Reference(mb => mb.Belt).LoadAsync(ct);
        return belt;
    }

    public async Task RemoveBeltAsync(MemberBelt belt, CancellationToken ct = default)
    {
        Context.MemberBelts.Remove(belt);
        await SaveChangesAsync(ct);
    }

    public async Task<bool> PromoteLatestBeltToCurrentAsync(string memberId, CancellationToken ct = default)
    {
        var latest = await Context.MemberBelts
            .Where(mb => mb.MemberId == memberId)
            .OrderByDescending(mb => mb.DateReceived).ThenByDescending(mb => mb.Id)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return false;

        latest.IsCurrentBelt = true;
        await SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// One projection for both the list and the profile. The current belt is read as three flat
    /// scalars rather than a nested object: it keeps the generated SQL obvious, and the belt is
    /// rebuilt into a <see cref="BeltDto"/> in <see cref="ToDto"/>.
    /// </summary>
    private static readonly Expression<Func<ApplicationUser, MemberRow>> Projection = u => new MemberRow(
        u.Id,
        u.FirstName,
        u.LastName,
        u.Email,
        u.PhoneNumber,
        u.DateOfBirth,
        u.StartedOn,
        u.IsActive,
        u.IsCoach,
        u.Height,
        u.Weight,
        u.Belts.Where(b => b.IsCurrentBelt).Select(b => (int?)b.Belt.Id).FirstOrDefault(),
        u.Belts.Where(b => b.IsCurrentBelt).Select(b => b.Belt.BeltName).FirstOrDefault(),
        u.Belts.Where(b => b.IsCurrentBelt).Select(b => (int?)b.Belt.Rank).FirstOrDefault(),
        u.Photo != null ? (Guid?)u.Photo.PublicId : null);

    private static MemberDto ToDto(MemberRow row, DateOnly today) => new(
        row.Id,
        row.FirstName,
        row.LastName,
        row.Email ?? string.Empty,
        row.PhoneNumber,
        row.DateOfBirth,
        AgeGroups.AgeOn(row.DateOfBirth, today),
        row.StartedOn,
        row.IsActive,
        row.IsCoach,
        row.Height,
        row.Weight,
        row.BeltId is int id && row.BeltName is not null
            ? new BeltDto(id, row.BeltName, row.BeltRank ?? 0)
            : null,
        row.PhotoPublicId);

    /// <summary>
    /// What the database returns. Age is added afterwards — deriving it needs today's date, and
    /// EF Core cannot translate a year subtraction over <c>DateOnly</c> on either provider.
    /// </summary>
    private sealed record MemberRow(
        string Id,
        string FirstName,
        string LastName,
        string? Email,
        string? PhoneNumber,
        DateOnly DateOfBirth,
        DateOnly StartedOn,
        bool IsActive,
        bool IsCoach,
        decimal? Height,
        decimal? Weight,
        int? BeltId,
        string? BeltName,
        int? BeltRank,
        Guid? PhotoPublicId);
}
