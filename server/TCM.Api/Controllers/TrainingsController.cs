using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Application.Dtos.Trainings;
using TCM.Application.Services;
using TCM.Domain.Constants;
using TCM.Domain.Enums;

namespace TCM.Api.Controllers;

/// <summary>
/// Trainings, invitations and attendance (SPEC sections 6.5 and 6.6). CRUD is coach-only; a
/// member reaches only the training they were invited to and their own attendance.
/// </summary>
[Authorize]
public class TrainingsController(ITrainingService trainingService) : BaseController
{
    /// <summary>The table view of SPEC section 6.5, filtered by title, status and type.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrainingDto>>>> Get(
        [FromQuery] string? title,
        [FromQuery] TrainingStatus? status,
        [FromQuery] TrainingType? type,
        CancellationToken ct)
        => HandleResult(await trainingService.GetTrainingsAsync(CallerId, title, status, type, ct));

    /// <summary>The calendar view of SPEC section 6.5, with invited/present counts per date.</summary>
    [HttpGet("calendar")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrainingDto>>>> GetCalendar(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => HandleResult(await trainingService.GetCalendarAsync(CallerId, year, month, ct));

    /// <summary>
    /// SPEC section 6.6. Open to both roles by attribute; the service then lets a member through
    /// only for a training they were actually invited to.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TrainingDetailsDto>>> GetDetails(int id, CancellationToken ct)
        => HandleResult(await trainingService.GetDetailsAsync(id, CallerId, IsCoach, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<TrainingDetailsDto>>> Create(
        [FromBody] EditTrainingDto dto, CancellationToken ct)
        => HandleResult(await trainingService.CreateAsync(dto, CallerId, ct));

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<TrainingDetailsDto>>> Update(
        int id, [FromBody] EditTrainingDto dto, CancellationToken ct)
        => HandleResult(await trainingService.UpdateAsync(id, dto, CallerId, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<Unit>>> Delete(int id, CancellationToken ct)
        => HandleResult(await trainingService.DeleteAsync(id, CallerId, ct));

    /// <summary>
    /// SPEC section 6.6: both the coach and the member report attendance. Omit
    /// <c>memberId</c> from the body to report for yourself, which is all a member may do.
    /// </summary>
    [HttpPost("{id:int}/attendance")]
    public async Task<ActionResult<ApiResponse<TrainingAttendeeDto>>> ReportAttendance(
        int id, [FromBody] ReportAttendanceDto dto, CancellationToken ct)
        => HandleResult(await trainingService.ReportAttendanceAsync(id, dto, CallerId, IsCoach, ct));

    /// <summary>
    /// The per-member performance score (SPEC sections 5 and 6.6). Coach only — a member may not
    /// score anyone, including themselves.
    /// </summary>
    [HttpPut("{id:int}/attendance/{memberId}/performance")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<TrainingAttendeeDto>>> SetPerformance(
        int id, string memberId, [FromBody] SetPerformanceDto dto, CancellationToken ct)
        => HandleResult(await trainingService.SetPerformanceAsync(id, memberId, dto, CallerId, ct));

    /// <summary>
    /// The attendance and performance charts of SPEC section 6.4. Lives on this controller
    /// rather than under <c>api/members</c> because the data is attendance, not member, data.
    /// </summary>
    [HttpGet("member/{memberId}/attendance")]
    public async Task<ActionResult<ApiResponse<MemberAttendanceSummaryDto>>> GetMemberAttendance(
        string memberId, [FromQuery] int? year, CancellationToken ct)
        => HandleResult(await trainingService.GetMemberAttendanceAsync(memberId, year, CallerId, IsCoach, ct));
}
