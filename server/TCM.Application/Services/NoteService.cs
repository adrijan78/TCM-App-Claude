using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Notes;
using TCM.Application.Options;
using TCM.Domain.Entities;

namespace TCM.Application.Services;

/// <summary>
/// Notes about members (SPEC sections 6.4, 6.6 and 6.8). Two rules shape this whole class:
/// the author of a note is always the caller's token id, and a member never reaches a note that
/// is not about them — except on delete, where the test is who wrote it (SPEC section 5).
/// </summary>
public class NoteService(
    INoteRepository notes,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IValidator<CreateNoteDto> createValidator,
    IOptions<ClientSettings> clientSettings,
    ILogger<NoteService> logger) : INoteService
{
    public async Task<ApiResponse<IReadOnlyList<NoteDto>>> GetClubNotesAsync(
        string callerId, bool isCoach, string? search, CancellationToken ct = default)
    {
        // The controller attribute already says Coach, but the rule is re-stated here so it
        // cannot be lost by a new caller that forgets the attribute.
        if (!isCoach)
        {
            return ApiResponse<IReadOnlyList<NoteDto>>.Forbidden();
        }

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller?.ClubId is null)
        {
            return ApiResponse<IReadOnlyList<NoteDto>>.Forbidden();
        }

        // Club comes from the coach's own account. Accepting one from the query string would let
        // any coach read another club's notes by changing a number in the URL.
        var results = await notes.GetForClubAsync(caller.ClubId, search, ct);

        return ApiResponse<IReadOnlyList<NoteDto>>.Ok(results);
    }

    public async Task<ApiResponse<IReadOnlyList<NoteDto>>> GetForMemberAsync(
        string memberId, string callerId, bool isCoach, string? search, CancellationToken ct = default)
    {
        var guard = await AuthorizeReadAsync(memberId, callerId, isCoach);
        if (guard is not null)
        {
            return guard;
        }

        var results = await notes.GetForMemberAsync(memberId, search, ct);

        return ApiResponse<IReadOnlyList<NoteDto>>.Ok(results);
    }

    public async Task<ApiResponse<IReadOnlyList<NoteDto>>> GetForTrainingAsync(
        int trainingId, string memberId, string callerId, bool isCoach, string? search, CancellationToken ct = default)
    {
        if (trainingId <= 0)
        {
            return ApiResponse<IReadOnlyList<NoteDto>>.Fail("A training must be supplied.");
        }

        var guard = await AuthorizeReadAsync(memberId, callerId, isCoach);
        if (guard is not null)
        {
            return guard;
        }

        var results = await notes.GetForTrainingAndMemberAsync(trainingId, memberId, search, ct);

        return ApiResponse<IReadOnlyList<NoteDto>>.Ok(results);
    }

    public async Task<ApiResponse<NoteDto>> CreateAsync(
        CreateNoteDto dto, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<NoteDto>();
        }

        // "Notes about another member" is coach-only; a member may write about themselves
        // (SPEC section 5). Checked against the token, never against anything in the body.
        if (!isCoach && dto.ToMemberId != callerId)
        {
            return ApiResponse<NoteDto>.Forbidden();
        }

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller is null)
        {
            return ApiResponse<NoteDto>.Forbidden();
        }

        var subject = await userManager.FindByIdAsync(dto.ToMemberId);
        if (subject is null)
        {
            return ApiResponse<NoteDto>.NotFound("Member not found.");
        }

        // 1 coach : 1 club (SPEC section 9), so a note never crosses a club boundary.
        if (caller.ClubId is null || caller.ClubId != subject.ClubId)
        {
            return ApiResponse<NoteDto>.Forbidden();
        }

        if (dto.TrainingId is int trainingId
            && !await notes.TrainingBelongsToClubAsync(trainingId, caller.ClubId, ct))
        {
            return ApiResponse<NoteDto>.NotFound("Training not found.");
        }

        var note = new Note
        {
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            // UTC DateTime, not DateTimeOffset: EF Core 10 cannot translate Year/Month on a
            // DateTimeOffset inside a GroupBy, and the club runs in a single time zone.
            CreatedAt = DateTime.UtcNow,
            FromMemberId = caller.Id,
            ToMemberId = subject.Id,
            TrainingId = dto.TrainingId,
            Priority = dto.Priority
        };

        await notes.AddAsync(note, ct);
        await notes.SaveChangesAsync(ct);

        // SPEC section 6.8: the member the note is about is emailed. Best-effort — a mail
        // failure must not undo a note that is already committed.
        await NotifySubjectAsync(note, caller, subject, ct);

        logger.LogInformation("Member {AuthorId} added note {NoteId} about {SubjectId}.",
            caller.Id, note.Id, subject.Id);

        var created = await notes.GetDtoAsync(note.Id, ct);

        return created is null
            ? ApiResponse<NoteDto>.Fail("The note was saved but could not be read back.")
            : ApiResponse<NoteDto>.Ok(created, "Note added.");
    }

    public async Task<ApiResponse<Unit>> DeleteAsync(
        int id, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var subject = await notes.GetSubjectAsync(id, ct);
        if (subject is null)
        {
            return ApiResponse.NotFound("Note not found.");
        }

        if (isCoach)
        {
            // A coach deletes anything in their own club, and nothing outside it.
            if (!await InSameClubAsync(callerId, subject.ToMemberClubId))
            {
                return ApiResponse.Forbidden();
            }
        }
        else if (subject.FromMemberId != callerId)
        {
            // SPEC section 5: "can delete only own notes" — own by authorship. A member who is
            // merely the subject of a coach's note cannot delete it.
            return ApiResponse.Forbidden();
        }

        var entity = await notes.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return ApiResponse.NotFound("Note not found.");
        }

        notes.Remove(entity);
        await notes.SaveChangesAsync(ct);

        logger.LogInformation("Note {NoteId} deleted by {CallerId}.", id, callerId);

        return ApiResponse.Ok("Note deleted.");
    }

    /// <summary>
    /// The read rule shared by the profile and training panels: a coach may read any member of
    /// their own club, a member only themselves. Returns null when the read is allowed.
    /// </summary>
    private async Task<ApiResponse<IReadOnlyList<NoteDto>>?> AuthorizeReadAsync(
        string memberId, string callerId, bool isCoach)
    {
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return ApiResponse<IReadOnlyList<NoteDto>>.Fail("A member must be supplied.");
        }

        if (!isCoach)
        {
            return memberId == callerId ? null : ApiResponse<IReadOnlyList<NoteDto>>.Forbidden();
        }

        var member = await userManager.FindByIdAsync(memberId);
        if (member is null)
        {
            return ApiResponse<IReadOnlyList<NoteDto>>.NotFound("Member not found.");
        }

        return await InSameClubAsync(callerId, member.ClubId)
            ? null
            : ApiResponse<IReadOnlyList<NoteDto>>.Forbidden();
    }

    private async Task<bool> InSameClubAsync(string callerId, int? clubId)
    {
        var caller = await userManager.FindByIdAsync(callerId);
        return caller is not null && caller.ClubId is not null && caller.ClubId == clubId;
    }

    private async Task NotifySubjectAsync(
        Note note, ApplicationUser author, ApplicationUser subject, CancellationToken ct)
    {
        // Nobody needs an email about a note they just wrote about themselves.
        if (subject.Id == author.Id || string.IsNullOrWhiteSpace(subject.Email))
        {
            return;
        }

        var link = BuildProfileLink(subject.Id);

        try
        {
            await emailService.SendAsync(new SendEmailRequest(
                subject.Email,
                $"{subject.FirstName} {subject.LastName}",
                $"New note: {note.Title}",
                $"""
                 <p>Hello {WebUtility.HtmlEncode(subject.FirstName)},</p>
                 <p>{WebUtility.HtmlEncode($"{author.FirstName} {author.LastName}")} added a note about you.</p>
                 <p><strong>{WebUtility.HtmlEncode(note.Title)}</strong> ({note.Priority} priority)</p>
                 <p>{WebUtility.HtmlEncode(note.Content)}</p>
                 <p><a href="{WebUtility.HtmlEncode(link)}">Open your profile</a></p>
                 """,
                $"Hello {subject.FirstName},\n\n{author.FirstName} {author.LastName} added a note about you.\n\n" +
                $"{note.Title} ({note.Priority} priority)\n{note.Content}\n\nOpen your profile: {link}"),
                ct);
        }
        catch (Exception ex)
        {
            // IEmailService is documented not to throw, but the note is already committed and
            // must survive an implementation that does.
            logger.LogWarning(ex, "Could not send the note notification for note {NoteId}.", note.Id);
        }
    }

    /// <summary>Client origin is configuration, never a hardcoded host (SPEC section 9).</summary>
    private string BuildProfileLink(string memberId)
    {
        var baseUrl = clientSettings.Value.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/dashboard/members/{Uri.EscapeDataString(memberId)}";
    }
}
