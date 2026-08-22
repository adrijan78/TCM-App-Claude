using TCM.Application.Dtos.Notes;
using TCM.Domain.Entities;

namespace TCM.Application.Abstractions;

/// <summary>
/// SPEC section 3.1 names NoteRepository explicitly. Every read here projects straight to
/// <see cref="NoteDto"/> in SQL — a note carries two user rows and optionally a training, and
/// loading those graphs to throw most of them away would cost far more than it returns.
/// </summary>
public interface INoteRepository : IRepository<Note>
{
    /// <summary>
    /// Every note about every member of one club (SPEC section 6.8). The club id comes from the
    /// calling coach's own account, never from the request.
    /// </summary>
    Task<IReadOnlyList<NoteDto>> GetForClubAsync(int? clubId, string? search, CancellationToken ct = default);

    /// <summary>
    /// The notes shown on one member's profile (SPEC section 6.4), ordered High priority first
    /// and newest first within a priority (section 6.8). The order is applied in SQL.
    /// </summary>
    Task<IReadOnlyList<NoteDto>> GetForMemberAsync(string memberId, string? search, CancellationToken ct = default);

    /// <summary>The notes panel for one member at one training (SPEC section 6.6).</summary>
    Task<IReadOnlyList<NoteDto>> GetForTrainingAndMemberAsync(
        int trainingId, string memberId, string? search, CancellationToken ct = default);

    /// <summary>A single note, already projected — used to return the row just created.</summary>
    Task<NoteDto?> GetDtoAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Who wrote a note and who it is about, without loading it. Drives the delete check.
    /// Returns null when no such note exists.
    /// </summary>
    Task<NoteSubject?> GetSubjectAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Whether a training exists and belongs to the given club. Guards the optional TrainingId
    /// on a new note so a note cannot be attached to another club's session.
    /// </summary>
    Task<bool> TrainingBelongsToClubAsync(int trainingId, int? clubId, CancellationToken ct = default);
}

/// <summary>
/// A note's author, its subject, and the club that subject belongs to. <c>FromMemberId</c> is
/// what the "can delete only own notes" rule in SPEC section 5 is measured against.
/// </summary>
public record NoteSubject(int NoteId, string FromMemberId, string ToMemberId, int? ToMemberClubId);
