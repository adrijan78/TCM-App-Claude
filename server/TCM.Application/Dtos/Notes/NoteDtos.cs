using TCM.Domain.Enums;

namespace TCM.Application.Dtos.Notes;

/// <summary>
/// SPEC section 3.1 — NoteDto. One note card, as shown on a member's profile (section 6.4), in
/// a training's notes panel (section 6.6) and on the club-wide notes page (section 6.8).
/// </summary>
public record NoteDto(
    int Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    NotePriority Priority,
    string FromMemberId,
    string FromMemberFullName,
    string ToMemberId,
    string ToMemberFullName,
    int? TrainingId,
    string? TrainingDescription);

/// <summary>
/// A new note. There is deliberately no author field: <c>FromMemberId</c> is always taken from
/// the caller's token, so a client cannot write a note under someone else's name.
/// </summary>
public record CreateNoteDto(
    string Title,
    string Content,
    NotePriority Priority,
    string ToMemberId,
    int? TrainingId);
