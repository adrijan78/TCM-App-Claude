using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Application.Dtos.Notes;
using TCM.Application.Services;
using TCM.Domain.Constants;

namespace TCM.Api.Controllers;

/// <summary>
/// Notes about members (SPEC sections 6.4, 6.6 and 6.8). Thin by design: the role attributes
/// here are the first half of the check, and <see cref="INoteService"/> holds the ownership
/// half that a member cannot get around by editing an id in the URL.
/// </summary>
[Authorize]
public class NotesController(INoteService noteService) : BaseController
{
    /// <summary>Club-wide notes page (SPEC section 6.8). Coach only.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NoteDto>>>> GetClubNotes(
        [FromQuery] string? search, CancellationToken ct)
        => HandleResult(await noteService.GetClubNotesAsync(CallerId, IsCoach, search, ct));

    /// <summary>
    /// The notes on a member's profile (SPEC section 6.4). A coach sees anyone in their club; a
    /// member sees only their own.
    /// </summary>
    [HttpGet("member/{memberId}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NoteDto>>>> GetForMember(
        string memberId, [FromQuery] string? search, CancellationToken ct)
        => HandleResult(await noteService.GetForMemberAsync(memberId, CallerId, IsCoach, search, ct));

    /// <summary>The notes panel for one member at one training (SPEC section 6.6).</summary>
    [HttpGet("training/{trainingId:int}/member/{memberId}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NoteDto>>>> GetForTraining(
        int trainingId, string memberId, [FromQuery] string? search, CancellationToken ct)
        => HandleResult(await noteService.GetForTrainingAsync(trainingId, memberId, CallerId, IsCoach, search, ct));

    /// <summary>
    /// Adds a note. The author is taken from the token, so <see cref="CreateNoteDto"/> has no
    /// field for it.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<NoteDto>>> Create(
        [FromBody] CreateNoteDto dto, CancellationToken ct)
        => HandleResult(await noteService.CreateAsync(dto, CallerId, IsCoach, ct));

    /// <summary>
    /// Deletes a note. A coach may delete any note in their club, a member only notes they
    /// wrote themselves (SPEC section 5).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<Unit>>> Delete(int id, CancellationToken ct)
        => HandleResult(await noteService.DeleteAsync(id, CallerId, IsCoach, ct));
}
