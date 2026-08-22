using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Trainings;
using TCM.Application.Options;
using TCM.Domain.Entities;
using TCM.Domain.Enums;

namespace TCM.Application.Services;

/// <summary>
/// Trainings, invitations and attendance (SPEC sections 6.5 and 6.6).
/// </summary>
/// <remarks>
/// The role attributes on <c>TrainingsController</c> are the first lock; this class is the
/// second. Every coach-only operation re-checks the caller's own account rather than trusting
/// the token's role claim alone, and every member-scoped operation compares the target id with
/// the caller's id. A member changing an id in a URL must never reach another member's data.
/// </remarks>
public class TrainingService(
    ITrainingRepository trainings,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IValidator<EditTrainingDto> editValidator,
    IValidator<ReportAttendanceDto> reportValidator,
    IValidator<SetPerformanceDto> performanceValidator,
    IOptions<ClientSettings> clientSettings,
    ILogger<TrainingService> logger) : ITrainingService
{
    public async Task<ApiResponse<IReadOnlyList<TrainingDto>>> GetTrainingsAsync(
        string callerId, string? title, TrainingStatus? status, TrainingType? type, CancellationToken ct = default)
    {
        var coach = await ResolveCoachAsync(callerId);
        if (coach is null)
        {
            return ApiResponse<IReadOnlyList<TrainingDto>>.Forbidden();
        }

        // The club comes from the coach's own account, never from the query string.
        var list = await trainings.GetForClubAsync(coach.Value.ClubId, title, status, type, ct);
        return ApiResponse<IReadOnlyList<TrainingDto>>.Ok(list);
    }

    public async Task<ApiResponse<IReadOnlyList<TrainingDto>>> GetCalendarAsync(
        string callerId, int? year, int? month, CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return ApiResponse<IReadOnlyList<TrainingDto>>.Fail("Month must be between 1 and 12.");
        }

        var coach = await ResolveCoachAsync(callerId);
        if (coach is null)
        {
            return ApiResponse<IReadOnlyList<TrainingDto>>.Forbidden();
        }

        var list = await trainings.GetCalendarAsync(coach.Value.ClubId, year, month, ct);
        return ApiResponse<IReadOnlyList<TrainingDto>>.Ok(list);
    }

    public async Task<ApiResponse<TrainingDetailsDto>> GetDetailsAsync(
        int trainingId, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var clubId = await trainings.GetClubIdAsync(trainingId, ct);

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller is null)
        {
            return ApiResponse<TrainingDetailsDto>.Forbidden();
        }

        if (clubId is null)
        {
            // A member gets the same answer whether the training does not exist or is not
            // theirs, so training ids cannot be enumerated. A coach still gets a useful 404.
            return isCoach && caller.IsCoach
                ? ApiResponse<TrainingDetailsDto>.NotFound("Training not found.")
                : ApiResponse<TrainingDetailsDto>.Forbidden();
        }

        if (isCoach && caller.IsCoach)
        {
            // A coach sees any training in their own club, and no other club's.
            if (caller.ClubId != clubId)
            {
                return ApiResponse<TrainingDetailsDto>.Forbidden();
            }
        }
        else
        {
            // A member sees a training only if they were invited to it (SPEC section 6.6).
            if (!await trainings.IsInvitedAsync(trainingId, callerId, ct))
            {
                return ApiResponse<TrainingDetailsDto>.Forbidden();
            }
        }

        var details = await trainings.GetDetailsAsync(trainingId, ct);
        if (details is null)
        {
            return ApiResponse<TrainingDetailsDto>.NotFound("Training not found.");
        }

        return ApiResponse<TrainingDetailsDto>.Ok(isCoach ? details : RedactPeers(details, callerId));
    }

    /// <summary>
    /// A member may see who else was invited (SPEC 6.6) but not their scores or absence reasons.
    /// SPEC section 5 gives the member "views own only" for attendance and performance, and an
    /// absence reason is free text — "hospital appointment" — about someone who may be a minor.
    /// The coach's view is unchanged.
    /// </summary>
    private static TrainingDetailsDto RedactPeers(TrainingDetailsDto details, string callerId) =>
        details with
        {
            Attendees = details.Attendees
                .Select(a => a.MemberId == callerId
                    ? a
                    : a with { Performance = null, AbsenceReason = null })
                .ToList()
        };

    public async Task<ApiResponse<TrainingDetailsDto>> CreateAsync(
        EditTrainingDto dto, string callerId, CancellationToken ct = default)
    {
        var validation = await editValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<TrainingDetailsDto>();
        }

        var coach = await ResolveCoachAsync(callerId);
        if (coach is null)
        {
            return ApiResponse<TrainingDetailsDto>.Forbidden();
        }

        var requested = Distinct(dto.MemberIds);
        var invitees = await trainings.GetClubMembersAsync(coach.Value.ClubId, requested, ct);

        if (invitees.Count != requested.Count)
        {
            // Either an unknown id or someone outside the coach's club. Both are the same
            // mistake from the screen's point of view, and neither may be silently dropped.
            return ApiResponse<TrainingDetailsDto>.Fail(
                "One or more of the selected members are not active members of your club.");
        }

        var date = ToUtc(dto.Date);

        var training = new Training
        {
            Description = dto.Description.Trim(),
            Date = date,
            TrainingType = dto.TrainingType,
            Status = dto.Status,
            // SPEC section 4 calls this column MemberId: the coach who created the training.
            MemberId = coach.Value.Coach.Id,
            ClubId = coach.Value.ClubId,
            Attendances = invitees
                .Select(i => new Attendance
                {
                    MemberId = i.MemberId,
                    Date = date,
                    Status = AttendanceStatus.Invited
                })
                .ToList()
        };

        await trainings.AddAsync(training, ct);
        await trainings.SaveChangesAsync(ct);

        logger.LogInformation(
            "Coach {CoachId} created training {TrainingId} with {InviteeCount} invitees.",
            callerId, training.Id, invitees.Count);

        await SendInvitationsAsync(training, invitees, ct);

        var details = await trainings.GetDetailsAsync(training.Id, ct);
        return ApiResponse<TrainingDetailsDto>.Ok(details!, "Training created and invitations sent.");
    }

    public async Task<ApiResponse<TrainingDetailsDto>> UpdateAsync(
        int trainingId, EditTrainingDto dto, string callerId, CancellationToken ct = default)
    {
        var validation = await editValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<TrainingDetailsDto>();
        }

        var coach = await ResolveCoachAsync(callerId);
        if (coach is null)
        {
            return ApiResponse<TrainingDetailsDto>.Forbidden();
        }

        var training = await trainings.GetWithAttendancesAsync(trainingId, ct);
        if (training is null)
        {
            return ApiResponse<TrainingDetailsDto>.NotFound("Training not found.");
        }

        if (training.ClubId != coach.Value.ClubId)
        {
            return ApiResponse<TrainingDetailsDto>.Forbidden();
        }

        var requested = Distinct(dto.MemberIds);
        var invitees = await trainings.GetClubMembersAsync(coach.Value.ClubId, requested, ct);

        if (invitees.Count != requested.Count)
        {
            return ApiResponse<TrainingDetailsDto>.Fail(
                "One or more of the selected members are not active members of your club.");
        }

        var date = ToUtc(dto.Date);

        training.Description = dto.Description.Trim();
        training.Date = date;
        training.TrainingType = dto.TrainingType;
        training.Status = dto.Status;

        var existingIds = training.Attendances.Select(a => a.MemberId).ToHashSet(StringComparer.Ordinal);

        var added = invitees.Where(i => !existingIds.Contains(i.MemberId)).ToList();
        foreach (var invitee in added)
        {
            training.Attendances.Add(new Attendance
            {
                MemberId = invitee.MemberId,
                Date = date,
                Status = AttendanceStatus.Invited
            });
        }

        var keptIds = requested.ToHashSet(StringComparer.Ordinal);

        // Uninviting someone drops their row only while nothing has been recorded on it.
        // A reported attendance or a performance score is history and is kept.
        var removable = training.Attendances
            .Where(a => !keptIds.Contains(a.MemberId))
            .Where(a => a.Status == AttendanceStatus.Invited && a.Performance is null)
            .ToList();

        var retained = training.Attendances
            .Count(a => !keptIds.Contains(a.MemberId) && (a.Status != AttendanceStatus.Invited || a.Performance is not null));

        foreach (var attendance in removable)
        {
            training.Attendances.Remove(attendance);
        }

        // Rows still on the invitee list keep pace with a rescheduled training.
        foreach (var attendance in training.Attendances.Where(a => keptIds.Contains(a.MemberId)))
        {
            attendance.Date = date;
        }

        // No Update() call: the graph came back tracked, so the change tracker already has the
        // edits, the added rows and — because Attendance.TrainingId is required — the severed
        // ones marked for deletion.
        await trainings.SaveChangesAsync(ct);

        logger.LogInformation(
            "Coach {CoachId} updated training {TrainingId}: {Added} invited, {Removed} uninvited, {Retained} kept for history.",
            callerId, training.Id, added.Count, removable.Count, retained);

        // Newly invited members get the same link the original invitees did, or they would have
        // no way of knowing they are expected.
        await SendInvitationsAsync(training, added, ct);

        var details = await trainings.GetDetailsAsync(training.Id, ct);
        return ApiResponse<TrainingDetailsDto>.Ok(details!, "Training updated.");
    }

    public async Task<ApiResponse<Unit>> DeleteAsync(int trainingId, string callerId, CancellationToken ct = default)
    {
        var coach = await ResolveCoachAsync(callerId);
        if (coach is null)
        {
            return ApiResponse.Forbidden();
        }

        var training = await trainings.GetWithAttendancesAsync(trainingId, ct);
        if (training is null)
        {
            return ApiResponse.NotFound("Training not found.");
        }

        if (training.ClubId != coach.Value.ClubId)
        {
            return ApiResponse.Forbidden();
        }

        // Attendance rows cascade with the training and notes written against it have their
        // TrainingId set to null, both by the schema — see AttendanceConfiguration and
        // NoteConfiguration. Nothing extra to clean up here.
        trainings.Remove(training);
        await trainings.SaveChangesAsync(ct);

        logger.LogInformation("Coach {CoachId} deleted training {TrainingId}.", callerId, trainingId);
        return ApiResponse.Ok("Training deleted.");
    }

    public async Task<ApiResponse<TrainingAttendeeDto>> ReportAttendanceAsync(
        int trainingId, ReportAttendanceDto dto, string callerId, bool isCoach, CancellationToken ct = default)
    {
        var validation = await reportValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<TrainingAttendeeDto>();
        }

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller is null)
        {
            return ApiResponse<TrainingAttendeeDto>.Forbidden();
        }

        var actingAsCoach = isCoach && caller.IsCoach;

        // An omitted member id means "for myself", which is all a member may ever do.
        var targetId = string.IsNullOrWhiteSpace(dto.MemberId) ? callerId : dto.MemberId!;

        if (!actingAsCoach && targetId != callerId)
        {
            logger.LogWarning(
                "Member {CallerId} tried to report attendance for {TargetId} on training {TrainingId}.",
                callerId, targetId, trainingId);
            return ApiResponse<TrainingAttendeeDto>.Forbidden();
        }

        var clubId = await trainings.GetClubIdAsync(trainingId, ct);
        if (clubId is null)
        {
            return ApiResponse<TrainingAttendeeDto>.NotFound("Training not found.");
        }

        if (actingAsCoach && caller.ClubId != clubId)
        {
            return ApiResponse<TrainingAttendeeDto>.Forbidden();
        }

        var attendance = await trainings.GetAttendanceAsync(trainingId, targetId, ct);
        if (attendance is null)
        {
            // For a member this is "you were not invited"; the wording is the same either way so
            // it cannot be used to probe who else is on the list.
            return actingAsCoach
                ? ApiResponse<TrainingAttendeeDto>.Fail("That member was not invited to this training.")
                : ApiResponse<TrainingAttendeeDto>.Forbidden();
        }

        attendance.Status = dto.Status;

        // The reason belongs to an absence. Marking someone present clears a stale one.
        attendance.Description = dto.Status == AttendanceStatus.Absent
            ? dto.AbsenceReason?.Trim()
            : null;

        // The row came back tracked from the repository, so saving is enough.
        await trainings.SaveChangesAsync(ct);

        var member = await userManager.FindByIdAsync(targetId);

        return ApiResponse<TrainingAttendeeDto>.Ok(
            new TrainingAttendeeDto(
                targetId,
                member?.FirstName ?? string.Empty,
                member?.LastName ?? string.Empty,
                attendance.Status,
                attendance.Description,
                attendance.Performance),
            "Attendance recorded.");
    }

    public async Task<ApiResponse<TrainingAttendeeDto>> SetPerformanceAsync(
        int trainingId, string memberId, SetPerformanceDto dto, string callerId, CancellationToken ct = default)
    {
        var validation = await performanceValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<TrainingAttendeeDto>();
        }

        // Coach only, checked against the caller's own account: SPEC section 5 gives no member
        // any way to set a performance score, not even their own.
        var coach = await ResolveCoachAsync(callerId);
        if (coach is null)
        {
            return ApiResponse<TrainingAttendeeDto>.Forbidden();
        }

        var clubId = await trainings.GetClubIdAsync(trainingId, ct);
        if (clubId is null)
        {
            return ApiResponse<TrainingAttendeeDto>.NotFound("Training not found.");
        }

        if (clubId != coach.Value.ClubId)
        {
            return ApiResponse<TrainingAttendeeDto>.Forbidden();
        }

        var attendance = await trainings.GetAttendanceAsync(trainingId, memberId, ct);
        if (attendance is null)
        {
            return ApiResponse<TrainingAttendeeDto>.Fail("That member was not invited to this training.");
        }

        attendance.Performance = dto.Performance;
        await trainings.SaveChangesAsync(ct);

        var member = await userManager.FindByIdAsync(memberId);

        logger.LogInformation(
            "Coach {CoachId} scored member {MemberId} at training {TrainingId}.", callerId, memberId, trainingId);

        return ApiResponse<TrainingAttendeeDto>.Ok(
            new TrainingAttendeeDto(
                memberId,
                member?.FirstName ?? string.Empty,
                member?.LastName ?? string.Empty,
                attendance.Status,
                attendance.Description,
                attendance.Performance),
            "Performance saved.");
    }

    public async Task<ApiResponse<MemberAttendanceSummaryDto>> GetMemberAttendanceAsync(
        string memberId, int? year, string callerId, bool isCoach, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return ApiResponse<MemberAttendanceSummaryDto>.Fail("A member id is required.");
        }

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller is null)
        {
            return ApiResponse<MemberAttendanceSummaryDto>.Forbidden();
        }

        var actingAsCoach = isCoach && caller.IsCoach;

        // A member reads their own charts and nobody else's.
        if (!actingAsCoach && callerId != memberId)
        {
            return ApiResponse<MemberAttendanceSummaryDto>.Forbidden();
        }

        var member = await userManager.FindByIdAsync(memberId);
        if (member is null)
        {
            return ApiResponse<MemberAttendanceSummaryDto>.NotFound("Member not found.");
        }

        if (actingAsCoach && (caller.ClubId is null || caller.ClubId != member.ClubId))
        {
            return ApiResponse<MemberAttendanceSummaryDto>.Forbidden();
        }

        var summary = await trainings.GetMemberAttendanceAsync(memberId, year, ct);
        return ApiResponse<MemberAttendanceSummaryDto>.Ok(summary);
    }

    /// <summary>
    /// Loads the caller and confirms they really are a coach with a club. The token's role claim
    /// got them past the attribute; this confirms it against the row that is the source of truth.
    /// </summary>
    private async Task<(ApplicationUser Coach, int ClubId)?> ResolveCoachAsync(string callerId)
    {
        var caller = await userManager.FindByIdAsync(callerId);

        if (caller is null || !caller.IsCoach || caller.ClubId is null)
        {
            return null;
        }

        return (caller, caller.ClubId.Value);
    }

    /// <summary>
    /// Trainings and attendance are stored as UTC (see <c>Training.Date</c>). A client that
    /// sends a local or unspecified time is normalised rather than silently stored as-is.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static IReadOnlyList<string> Distinct(IReadOnlyList<string> memberIds) =>
        memberIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// SPEC section 6.5: every invited member is emailed a link to the details screen so they can
    /// mark attendance. Best-effort — a training must not be lost because SMTP was unavailable.
    /// </summary>
    private async Task SendInvitationsAsync(
        Training training, IReadOnlyList<TrainingInviteeDto> invitees, CancellationToken ct)
    {
        if (invitees.Count == 0) return;

        var link = BuildTrainingLink(training.Id);
        var when = training.Date.ToString("dddd d MMMM yyyy, HH:mm 'UTC'");

        foreach (var invitee in invitees)
        {
            if (string.IsNullOrWhiteSpace(invitee.Email)) continue;

            try
            {
                await emailService.SendAsync(new SendEmailRequest(
                    invitee.Email,
                    $"{invitee.FirstName} {invitee.LastName}",
                    $"Training invitation: {training.Description}",
                    $"""
                     <p>Hello {WebUtility.HtmlEncode(invitee.FirstName)},</p>
                     <p>You are invited to <strong>{WebUtility.HtmlEncode(training.Description)}</strong> on {WebUtility.HtmlEncode(when)}.</p>
                     <p><a href="{WebUtility.HtmlEncode(link)}">Open the training and confirm your attendance</a></p>
                     """,
                    $"Hello {invitee.FirstName},\n\nYou are invited to {training.Description} on {when}.\n\nConfirm your attendance: {link}"),
                    ct);
            }
            catch (Exception ex)
            {
                // Deliberately swallowed. The training exists; the invitation is the soft part.
                logger.LogWarning(ex,
                    "Could not email the invitation for training {TrainingId} to member {MemberId}.",
                    training.Id, invitee.MemberId);
            }
        }
    }

    private string BuildTrainingLink(int trainingId)
    {
        // Client origin is configuration, never a hardcoded host (SPEC section 9).
        var baseUrl = clientSettings.Value.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/dashboard/trainings/{trainingId}";
    }
}
