using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
using TCM.Application.Dtos.Trainings;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure.Repositories;

public class TrainingRepository(ApplicationDbContext context) : Repository<Training>(context), ITrainingRepository
{
    public async Task<IReadOnlyList<TrainingDto>> GetForClubAsync(
        int clubId, string? title, TrainingStatus? status, TrainingType? type, CancellationToken ct = default)
    {
        // Every filter is applied to the IQueryable, so the database does the narrowing. The
        // table view of SPEC section 6.5 filters by title, status and type.
        var query = Context.Trainings
            .AsNoTracking()
            .Where(t => t.ClubId == clubId);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var term = title.Trim();
            query = query.Where(t => t.Description.Contains(term));
        }

        if (status is not null) query = query.Where(t => t.Status == status);
        if (type is not null) query = query.Where(t => t.TrainingType == type);

        return await query
            .OrderByDescending(t => t.Date)
            .Select(t => new TrainingDto(
                t.Id,
                t.Date,
                t.Description,
                t.TrainingType,
                t.Status,
                t.Attendances.Count,
                t.Attendances.Count(a => a.Status == AttendanceStatus.Present)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TrainingDto>> GetCalendarAsync(
        int clubId, int? year, int? month, CancellationToken ct = default) =>
        await Context.Trainings
            .AsNoTracking()
            .Where(t => t.ClubId == clubId)
            // Date is a UTC DateTime, not a DateTimeOffset, precisely so EF can translate these.
            .Where(t => year == null || t.Date.Year == year)
            .Where(t => month == null || t.Date.Month == month)
            .OrderBy(t => t.Date)
            .Select(t => new TrainingDto(
                t.Id,
                t.Date,
                t.Description,
                t.TrainingType,
                t.Status,
                t.Attendances.Count,
                t.Attendances.Count(a => a.Status == AttendanceStatus.Present)))
            .ToListAsync(ct);

    public async Task<TrainingDetailsDto?> GetDetailsAsync(int trainingId, CancellationToken ct = default)
    {
        var training = await Context.Trainings
            .AsNoTracking()
            .Where(t => t.Id == trainingId)
            .Select(t => new
            {
                t.Id,
                t.Date,
                t.Description,
                t.TrainingType,
                t.Status,
                Attendees = t.Attendances
                    .OrderBy(a => a.Member.FirstName).ThenBy(a => a.Member.LastName)
                    .Select(a => new TrainingAttendeeDto(
                        a.MemberId,
                        a.Member.FirstName,
                        a.Member.LastName,
                        a.Status,
                        a.Description,
                        a.Performance))
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        return training is null
            ? null
            : new TrainingDetailsDto(
                training.Id, training.Date, training.Description,
                training.TrainingType, training.Status, training.Attendees);
    }

    /// <summary>Tracked on purpose — the edit path reconciles this collection in place.</summary>
    public async Task<Training?> GetWithAttendancesAsync(int trainingId, CancellationToken ct = default) =>
        await Context.Trainings
            .Include(t => t.Attendances)
            .FirstOrDefaultAsync(t => t.Id == trainingId, ct);

    public async Task<int?> GetClubIdAsync(int trainingId, CancellationToken ct = default) =>
        await Context.Trainings
            .AsNoTracking()
            .Where(t => t.Id == trainingId)
            .Select(t => (int?)t.ClubId)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> IsInvitedAsync(int trainingId, string memberId, CancellationToken ct = default) =>
        await Context.Attendances
            .AsNoTracking()
            .AnyAsync(a => a.TrainingId == trainingId && a.MemberId == memberId, ct);

    public async Task<Attendance?> GetAttendanceAsync(
        int trainingId, string memberId, CancellationToken ct = default) =>
        await Context.Attendances
            .FirstOrDefaultAsync(a => a.TrainingId == trainingId && a.MemberId == memberId, ct);

    public async Task<IReadOnlyList<TrainingInviteeDto>> GetClubMembersAsync(
        int clubId, IReadOnlyCollection<string> memberIds, CancellationToken ct = default)
    {
        if (memberIds.Count == 0) return [];

        return await Context.Users
            .AsNoTracking()
            .Where(u => u.ClubId == clubId && u.IsActive && memberIds.Contains(u.Id))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new TrainingInviteeDto(u.Id, u.FirstName, u.LastName, u.Email))
            .ToListAsync(ct);
    }

    public async Task<MemberAttendanceSummaryDto> GetMemberAttendanceAsync(
        string memberId, int? year, CancellationToken ct = default)
    {
        var rows = Context.Attendances
            .AsNoTracking()
            .Where(a => a.MemberId == memberId)
            .Where(a => year == null || a.Training.Date.Year == year);

        var invitedCount = await rows.CountAsync(ct);
        var presentCount = await rows.CountAsync(a => a.Status == AttendanceStatus.Present, ct);
        var absentCount = await rows.CountAsync(a => a.Status == AttendanceStatus.Absent, ct);

        // Present as a share of everything the member was invited to, matching how the
        // dashboard computes the club-wide figure.
        var attendancePercentage = invitedCount == 0
            ? 0d
            : Math.Round(presentCount * 100d / invitedCount, 1);

        // Grouped into an anonymous type: EF cannot project straight into a record's
        // constructor from inside a GroupBy, on SQL Server or SQLite.
        var perMonthRows = await rows
            .GroupBy(a => new { a.Training.Date.Year, a.Training.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Invited = g.Count(),
                Present = g.Count(a => a.Status == AttendanceStatus.Present),
                Absent = g.Count(a => a.Status == AttendanceStatus.Absent)
            })
            .ToListAsync(ct);

        var perMonth = perMonthRows
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .Select(r => new MonthlyAttendanceDto(r.Year, r.Month, r.Invited, r.Present, r.Absent))
            .ToList();

        var trainings = await rows
            .OrderBy(a => a.Training.Date)
            .Select(a => new MemberTrainingDto(
                a.TrainingId,
                a.Training.Date,
                a.Training.Description,
                a.Training.TrainingType,
                a.Training.Status,
                a.Status,
                a.Description,
                a.Performance))
            .ToListAsync(ct);

        return new MemberAttendanceSummaryDto(
            memberId, year, invitedCount, presentCount, absentCount, attendancePercentage, perMonth, trainings);
    }
}
