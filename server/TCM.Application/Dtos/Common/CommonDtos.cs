namespace TCM.Application.Dtos.Common;

/// <summary>SPEC section 3.1 — BeltDto. Populates every belt dropdown and filter.</summary>
public record BeltDto(int Id, string BeltName, int Rank);

/// <summary>
/// SPEC sections 3.1 and 6.2 — the dashboard's stat cards and the trainings-per-month chart.
/// </summary>
public record ClubNumbersInfoDto(
    int TotalMembers,
    int ActiveMembers,
    int TrainingsHeld,
    double AttendancePercentage,
    IReadOnlyList<MonthlyTrainingCountDto> TrainingsPerMonth);

public record MonthlyTrainingCountDto(int Year, int Month, int Count);
