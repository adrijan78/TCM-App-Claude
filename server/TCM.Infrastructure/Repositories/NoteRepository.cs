using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
using TCM.Application.Dtos.Notes;
using TCM.Domain.Entities;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure.Repositories;

public class NoteRepository(ApplicationDbContext context) : Repository<Note>(context), INoteRepository
{
    public async Task<IReadOnlyList<NoteDto>> GetForClubAsync(
        int? clubId, string? search, CancellationToken ct = default) =>
        await Ordered(Filtered(BaseQuery(), search).Where(n => clubId == null || n.ToMember.ClubId == clubId))
            .Select(Projection)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NoteDto>> GetForMemberAsync(
        string memberId, string? search, CancellationToken ct = default) =>
        await Ordered(Filtered(BaseQuery(), search).Where(n => n.ToMemberId == memberId))
            .Select(Projection)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NoteDto>> GetForTrainingAndMemberAsync(
        int trainingId, string memberId, string? search, CancellationToken ct = default) =>
        await Ordered(Filtered(BaseQuery(), search)
                .Where(n => n.ToMemberId == memberId && n.TrainingId == trainingId))
            .Select(Projection)
            .ToListAsync(ct);

    public async Task<NoteDto?> GetDtoAsync(int id, CancellationToken ct = default) =>
        await BaseQuery()
            .Where(n => n.Id == id)
            .Select(Projection)
            .FirstOrDefaultAsync(ct);

    public async Task<NoteSubject?> GetSubjectAsync(int id, CancellationToken ct = default) =>
        await BaseQuery()
            .Where(n => n.Id == id)
            .Select(n => new NoteSubject(n.Id, n.FromMemberId, n.ToMemberId, n.ToMember.ClubId))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TrainingBelongsToClubAsync(
        int trainingId, int? clubId, CancellationToken ct = default) =>
        await Context.Trainings
            .AsNoTracking()
            .AnyAsync(t => t.Id == trainingId && (clubId == null || t.ClubId == clubId), ct);

    private IQueryable<Note> BaseQuery() => Context.Notes.AsNoTracking();

    /// <summary>
    /// Search by title (SPEC sections 6.4, 6.6 and 6.8). Translated to a LIKE, so the filtering
    /// happens in the database rather than over a materialised list.
    /// </summary>
    private static IQueryable<Note> Filtered(IQueryable<Note> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;

        // Trimmed outside the expression so EF parameterises a plain string rather than having
        // to reason about a method call on the captured variable.
        var term = search.Trim();
        return query.Where(n => n.Title.Contains(term));
    }

    /// <summary>
    /// The order SPEC section 6.8 specifies: High, then Medium, then Low, newest first inside a
    /// priority. NotePriority's numeric values ascend with importance, so descending is correct.
    /// </summary>
    private static IQueryable<Note> Ordered(IQueryable<Note> query) =>
        query.OrderByDescending(n => n.Priority).ThenByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id);

    /// <summary>
    /// One projection shared by every read, so all four endpoints return the same shape and no
    /// entity ever leaves the data layer.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<Note, NoteDto>> Projection =>
        n => new NoteDto(
            n.Id,
            n.Title,
            n.Content,
            n.CreatedAt,
            n.Priority,
            n.FromMemberId,
            n.FromMember.FirstName + " " + n.FromMember.LastName,
            n.ToMemberId,
            n.ToMember.FirstName + " " + n.ToMember.LastName,
            n.TrainingId,
            n.Training == null ? null : n.Training.Description);
}
