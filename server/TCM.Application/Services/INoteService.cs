using TCM.Application.Common;
using TCM.Application.Dtos.Notes;

namespace TCM.Application.Services;

public interface INoteService
{
    /// <summary>
    /// Every note in the coach's own club (SPEC section 6.8). Coach only, and scoped to the club
    /// on the caller's account rather than to any club id supplied by the client.
    /// </summary>
    Task<ApiResponse<IReadOnlyList<NoteDto>>> GetClubNotesAsync(
        string callerId, bool isCoach, string? search, CancellationToken ct = default);

    /// <summary>
    /// The notes on one member's profile (SPEC section 6.4). A coach may read anyone in their
    /// club; a member may read only their own.
    /// </summary>
    Task<ApiResponse<IReadOnlyList<NoteDto>>> GetForMemberAsync(
        string memberId, string callerId, bool isCoach, string? search, CancellationToken ct = default);

    /// <summary>The notes panel for one member at one training (SPEC section 6.6).</summary>
    Task<ApiResponse<IReadOnlyList<NoteDto>>> GetForTrainingAsync(
        int trainingId, string memberId, string callerId, bool isCoach, string? search, CancellationToken ct = default);

    /// <summary>
    /// Writes a note. The author is always <paramref name="callerId"/>. A coach may write about
    /// anyone in their club; a member may write only about themselves (SPEC section 5).
    /// </summary>
    Task<ApiResponse<NoteDto>> CreateAsync(
        CreateNoteDto dto, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>
    /// Deletes a note. A coach may delete any note in their club; a member may delete only notes
    /// they wrote — SPEC section 5's "can delete only own notes" is about authorship.
    /// </summary>
    Task<ApiResponse<Unit>> DeleteAsync(int id, string callerId, bool isCoach, CancellationToken ct = default);
}
