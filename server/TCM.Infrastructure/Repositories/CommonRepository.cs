using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
using TCM.Application.Dtos.Common;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure.Repositories;

public class CommonRepository(ApplicationDbContext context) : Repository<Belt>(context), ICommonRepository
{
    public async Task<IReadOnlyList<BeltDto>> GetBeltsAsync(CancellationToken ct = default) =>
        await Context.Belts
            .AsNoTracking()
            .OrderBy(b => b.Rank)
            .Select(b => new BeltDto(b.Id, b.BeltName, b.Rank))
            .ToListAsync(ct);

    public async Task<ClubNumbersInfoDto> GetClubNumbersAsync(
        int? clubId, int? year, int? month, CancellationToken ct = default)
    {
        var members = Context.Users.AsNoTracking().Where(u => clubId == null || u.ClubId == clubId);

        var totalMembers = await members.CountAsync(ct);
        var activeMembers = await members.CountAsync(u => u.IsActive, ct);

        var trainings = Context.Trainings
            .AsNoTracking()
            .Where(t => clubId == null || t.ClubId == clubId);

        // "Trainings held" means finished ones — an active or cancelled session has not happened.
        var heldTrainings = trainings.Where(t => t.Status == TrainingStatus.Finished);

        var filteredHeld = heldTrainings
            .Where(t => year == null || t.Date.Year == year)
            .Where(t => month == null || t.Date.Month == month);

        var trainingsHeld = await filteredHeld.CountAsync(ct);

        // Attendance percentage over the same filtered set: present as a share of everyone who
        // was invited. Navigating through a.Training keeps this one translatable join rather
        // than a correlated subquery over another IQueryable.
        var attendanceRows = Context.Attendances
            .AsNoTracking()
            .Where(a => a.Training.Status == TrainingStatus.Finished)
            .Where(a => clubId == null || a.Training.ClubId == clubId)
            .Where(a => year == null || a.Training.Date.Year == year)
            .Where(a => month == null || a.Training.Date.Month == month);

        var invitedCount = await attendanceRows.CountAsync(ct);
        var presentCount = await attendanceRows.CountAsync(a => a.Status == AttendanceStatus.Present, ct);

        var attendancePercentage = invitedCount == 0
            ? 0d
            : Math.Round(presentCount * 100d / invitedCount, 1);

        // Per-month counts for the dashboard chart, restricted to the selected year when given.
        // Grouped into an anonymous type: EF cannot translate a projection straight into a
        // record's constructor from inside a GroupBy.
        var perMonthRows = await heldTrainings
            .Where(t => year == null || t.Date.Year == year)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        var perMonth = perMonthRows
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .Select(r => new MonthlyTrainingCountDto(r.Year, r.Month, r.Count))
            .ToList();

        return new ClubNumbersInfoDto(totalMembers, activeMembers, trainingsHeld, attendancePercentage, perMonth);
    }
}
