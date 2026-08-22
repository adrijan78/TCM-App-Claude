using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Dtos.Trainings;
using TCM.Domain.Enums;

namespace TCM.Tests.Integration;

/// <summary>
/// SPEC sections 6.5 and 6.6 read as a test matrix. Every route is exercised anonymously, as a
/// member and as a coach, and the two rules that matter most are asserted explicitly: a member
/// may never report attendance for someone else, and a member may never set a performance
/// score — not even their own.
/// </summary>
public class TrainingsEndpointTests(TcmApiFactory factory) : IClassFixture<TcmApiFactory>
{
    private async Task<HttpClient> ClientAsAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/account/login", new LoginMemberDto(email, TcmApiFactory.Password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        Assert.NotNull(body?.Data);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return client;
    }

    /// <summary>Creates a training as the coach and returns it, failing the test if it did not work.</summary>
    private async Task<TrainingDetailsDto> CreateTrainingAsync(
        HttpClient coach,
        string description,
        IReadOnlyList<string> memberIds,
        TrainingStatus status = TrainingStatus.Active,
        DateTime? date = null)
    {
        var response = await coach.PostAsJsonAsync("/api/trainings", new EditTrainingDto(
            description,
            date ?? new DateTime(2026, 3, 4, 18, 0, 0, DateTimeKind.Utc),
            TrainingType.Regular,
            status,
            memberIds));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingDetailsDto>>();
        Assert.True(body!.Success);
        return body.Data!;
    }

    // ---- GET /api/trainings (coach only) --------------------------------------------------------

    [Fact]
    public async Task ListTrainings_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/trainings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListTrainings_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync("/api/trainings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListTrainings_AsCoach_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await CreateTrainingAsync(coach, "Listed session", [factory.MemberId]);

        var response = await coach.GetAsync("/api/trainings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<TrainingDto>>>();
        Assert.True(body!.Success);
        Assert.Contains(body.Data!, t => t.Description == "Listed session");
    }

    [Fact]
    public async Task ListTrainings_FilteredByTitleStatusAndType_NarrowsTheResult()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await CreateTrainingAsync(coach, "Kicking drills alpha", [factory.MemberId], TrainingStatus.Finished);
        await CreateTrainingAsync(coach, "Poomsae review beta", [factory.MemberId]);

        var response = await coach.GetAsync("/api/trainings?title=alpha&status=Finished&type=Regular");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<TrainingDto>>>();
        Assert.True(body!.Success);
        Assert.NotEmpty(body.Data!);
        Assert.All(body.Data!, t =>
        {
            Assert.Contains("alpha", t.Description);
            Assert.Equal(TrainingStatus.Finished, t.Status);
            Assert.Equal(TrainingType.Regular, t.TrainingType);
        });
    }

    // ---- GET /api/trainings/calendar (coach only) -----------------------------------------------

    [Fact]
    public async Task Calendar_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/trainings/calendar?year=2026&month=3");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Calendar_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync("/api/trainings/calendar?year=2026&month=3");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Calendar_AsCoach_CarriesInvitedAndPresentCounts()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(
            coach, "Counted session", [factory.MemberId, factory.OtherMemberId],
            date: new DateTime(2026, 7, 9, 18, 0, 0, DateTimeKind.Utc));

        await coach.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(factory.MemberId, AttendanceStatus.Present, null));

        var response = await coach.GetAsync("/api/trainings/calendar?year=2026&month=7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<TrainingDto>>>();
        var entry = Assert.Single(body!.Data!, t => t.Id == training.Id);
        Assert.Equal(2, entry.InvitedCount);
        Assert.Equal(1, entry.PresentCount);
    }

    [Fact]
    public async Task Calendar_WithImpossibleMonth_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/trainings/calendar?year=2026&month=13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- POST /api/trainings (coach only) -------------------------------------------------------

    [Fact]
    public async Task CreateTraining_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/trainings", new EditTrainingDto(
            "Nope", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active, []));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTraining_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PostAsJsonAsync("/api/trainings", new EditTrainingDto(
            "Members cannot schedule", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active,
            [factory.MemberId]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTraining_AsCoach_InvitesEveryMemberAsInvited()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var training = await CreateTrainingAsync(
            coach, "Sparring night", [factory.MemberId, factory.OtherMemberId]);

        Assert.Equal(2, training.Attendees.Count);
        Assert.All(training.Attendees, a => Assert.Equal(AttendanceStatus.Invited, a.Status));
        Assert.All(training.Attendees, a => Assert.Null(a.Performance));
    }

    [Fact]
    public async Task CreateTraining_WithAnUnknownInvitee_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync("/api/trainings", new EditTrainingDto(
            "Ghost invite", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active,
            [factory.MemberId, "not-a-real-user-id"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingDetailsDto>>();
        Assert.False(body!.Success);
    }

    [Fact]
    public async Task CreateTraining_WithNoDescription_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync("/api/trainings", new EditTrainingDto(
            "  ", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active, [factory.MemberId]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingDetailsDto>>();
        Assert.False(body!.Success);
        Assert.NotEmpty(body.Errors);
    }

    // ---- GET /api/trainings/{id} ----------------------------------------------------------------

    [Fact]
    public async Task TrainingDetails_Anonymously_Returns401()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Private details", [factory.MemberId]);

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/trainings/{training.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TrainingDetails_AsInvitedMember_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Invited member may look", [factory.MemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.GetAsync($"/api/trainings/{training.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingDetailsDto>>();
        Assert.True(body!.Success);
        Assert.Contains(body.Data!.Attendees, a => a.MemberId == factory.MemberId);
    }

    [Fact]
    public async Task TrainingDetails_AsUninvitedMember_Returns403()
    {
        // The invitation is the whole permission. Without a row on this training the member is
        // just another stranger, even though the training belongs to their own club.
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Not for the other one", [factory.MemberId]);

        var uninvited = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);
        var response = await uninvited.GetAsync($"/api/trainings/{training.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingDetailsDto>>();
        Assert.False(body!.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task TrainingDetails_AsCoach_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Coach may look", [factory.OtherMemberId]);

        var response = await coach.GetAsync($"/api/trainings/{training.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TrainingDetails_ForAnIdThatDoesNotExist_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/trainings/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- PUT /api/trainings/{id} (coach only) ---------------------------------------------------

    [Fact]
    public async Task UpdateTraining_AsMember_Returns403()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Members cannot edit", [factory.MemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.PutAsJsonAsync($"/api/trainings/{training.Id}", new EditTrainingDto(
            "Hijacked", training.Date, TrainingType.Sparring, TrainingStatus.Cancelled, [factory.MemberId]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTraining_AsCoach_AddsAndRemovesInvitees()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Reconciled session", [factory.MemberId]);

        var response = await coach.PutAsJsonAsync($"/api/trainings/{training.Id}", new EditTrainingDto(
            "Reconciled session, renamed",
            training.Date,
            TrainingType.Sparring,
            TrainingStatus.Finished,
            [factory.OtherMemberId]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingDetailsDto>>();
        Assert.True(body!.Success);
        Assert.Equal("Reconciled session, renamed", body.Data!.Description);
        Assert.Equal(TrainingType.Sparring, body.Data.TrainingType);
        Assert.Equal(TrainingStatus.Finished, body.Data.Status);

        // The dropped invitee had reported nothing, so their row goes with them.
        var attendee = Assert.Single(body.Data.Attendees);
        Assert.Equal(factory.OtherMemberId, attendee.MemberId);
    }

    [Fact]
    public async Task UpdateTraining_KeepsAnUninvitedMemberWhoAlreadyReported()
    {
        // History wins over the invitee list: removing a row that carries a reported attendance
        // would quietly erase the fact that the member turned up.
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(
            coach, "History is kept", [factory.MemberId, factory.OtherMemberId]);

        await coach.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(factory.MemberId, AttendanceStatus.Present, null));

        var response = await coach.PutAsJsonAsync($"/api/trainings/{training.Id}", new EditTrainingDto(
            "History is kept", training.Date, TrainingType.Regular, TrainingStatus.Finished,
            [factory.OtherMemberId]));

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingDetailsDto>>();
        Assert.True(body!.Success);
        Assert.Equal(2, body.Data!.Attendees.Count);
        Assert.Contains(body.Data.Attendees,
            a => a.MemberId == factory.MemberId && a.Status == AttendanceStatus.Present);
    }

    // ---- DELETE /api/trainings/{id} (coach only) ------------------------------------------------

    [Fact]
    public async Task DeleteTraining_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/trainings/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTraining_AsMember_Returns403()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Members cannot delete", [factory.MemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.DeleteAsync($"/api/trainings/{training.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTraining_AsCoach_RemovesItAndItsInvitations()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Cancelled for good", [factory.MemberId]);

        var deleted = await coach.DeleteAsync($"/api/trainings/{training.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var refetch = await coach.GetAsync($"/api/trainings/{training.Id}");
        Assert.Equal(HttpStatusCode.NotFound, refetch.StatusCode);
    }

    // ---- POST /api/trainings/{id}/attendance ----------------------------------------------------

    [Fact]
    public async Task ReportAttendance_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/trainings/1/attendance",
            new ReportAttendanceDto(null, AttendanceStatus.Present, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReportAttendance_ForSelf_AsInvitedMember_IsRecorded()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Self report", [factory.MemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(null, AttendanceStatus.Absent, "Away at a tournament"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingAttendeeDto>>();
        Assert.True(body!.Success);
        Assert.Equal(factory.MemberId, body.Data!.MemberId);
        Assert.Equal(AttendanceStatus.Absent, body.Data.Status);
        Assert.Equal("Away at a tournament", body.Data.AbsenceReason);
    }

    [Fact]
    public async Task ReportAttendance_ForAnotherMember_AsMember_Returns403()
    {
        // The single most likely security bug in this app: a member changing an id in the body
        // and writing against somebody else's record.
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(
            coach, "No reporting for others", [factory.MemberId, factory.OtherMemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(factory.OtherMemberId, AttendanceStatus.Present, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // And nothing was written: the other member is still merely invited.
        var details = await coach.GetFromJsonAsync<ApiResponse<TrainingDetailsDto>>($"/api/trainings/{training.Id}");
        var other = Assert.Single(details!.Data!.Attendees, a => a.MemberId == factory.OtherMemberId);
        Assert.Equal(AttendanceStatus.Invited, other.Status);
    }

    [Fact]
    public async Task ReportAttendance_ForSelf_AsUninvitedMember_Returns403()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Uninvited cannot report", [factory.MemberId]);

        var uninvited = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);
        var response = await uninvited.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(null, AttendanceStatus.Present, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReportAttendance_AsCoach_ForAnyInvitedMember_IsRecorded()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Coach reports for all", [factory.OtherMemberId]);

        var response = await coach.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(factory.OtherMemberId, AttendanceStatus.Present, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingAttendeeDto>>();
        Assert.Equal(AttendanceStatus.Present, body!.Data!.Status);
        // A present member carries no absence reason, even if one was sent.
        Assert.Null(body.Data.AbsenceReason);
    }

    [Fact]
    public async Task ReportAttendance_AbsentWithoutAReason_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Reason required", [factory.MemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(null, AttendanceStatus.Absent, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- PUT /api/trainings/{id}/attendance/{memberId}/performance (coach only) -----------------

    [Fact]
    public async Task SetPerformance_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/trainings/1/attendance/{factory.MemberId}/performance", new SetPerformanceDto(9));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetPerformance_AsMember_ForThemselves_Returns403()
    {
        // SPEC section 5 gives a member no way to score anyone at all, their own row included.
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "No self scoring", [factory.MemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.PutAsJsonAsync(
            $"/api/trainings/{training.Id}/attendance/{factory.MemberId}/performance", new SetPerformanceDto(10));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var details = await coach.GetFromJsonAsync<ApiResponse<TrainingDetailsDto>>($"/api/trainings/{training.Id}");
        var row = Assert.Single(details!.Data!.Attendees, a => a.MemberId == factory.MemberId);
        Assert.Null(row.Performance);
    }

    [Fact]
    public async Task SetPerformance_AsMember_ForAnotherMember_Returns403()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(
            coach, "No scoring others", [factory.MemberId, factory.OtherMemberId]);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.PutAsJsonAsync(
            $"/api/trainings/{training.Id}/attendance/{factory.OtherMemberId}/performance",
            new SetPerformanceDto(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetPerformance_AsCoach_IsRecorded()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Scored session", [factory.MemberId]);

        var response = await coach.PutAsJsonAsync(
            $"/api/trainings/{training.Id}/attendance/{factory.MemberId}/performance", new SetPerformanceDto(8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingAttendeeDto>>();
        Assert.True(body!.Success);
        Assert.Equal(8, body.Data!.Performance);
    }

    [Fact]
    public async Task SetPerformance_OutsideTheAllowedRange_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Out of range", [factory.MemberId]);

        var response = await coach.PutAsJsonAsync(
            $"/api/trainings/{training.Id}/attendance/{factory.MemberId}/performance", new SetPerformanceDto(99));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetPerformance_ForAMemberWhoWasNotInvited_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(coach, "Only one invitee", [factory.MemberId]);

        var response = await coach.PutAsJsonAsync(
            $"/api/trainings/{training.Id}/attendance/{factory.OtherMemberId}/performance",
            new SetPerformanceDto(5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TrainingAttendeeDto>>();
        Assert.False(body!.Success);
    }

    // ---- GET /api/trainings/member/{memberId}/attendance ----------------------------------------

    [Fact]
    public async Task MemberAttendance_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/trainings/member/{factory.MemberId}/attendance");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MemberAttendance_ForSelf_AsMember_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var training = await CreateTrainingAsync(
            coach, "Charted session", [factory.MemberId],
            TrainingStatus.Finished, new DateTime(2026, 5, 6, 18, 0, 0, DateTimeKind.Utc));

        await coach.PostAsJsonAsync($"/api/trainings/{training.Id}/attendance",
            new ReportAttendanceDto(factory.MemberId, AttendanceStatus.Present, null));
        await coach.PutAsJsonAsync(
            $"/api/trainings/{training.Id}/attendance/{factory.MemberId}/performance", new SetPerformanceDto(7));

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.GetAsync($"/api/trainings/member/{factory.MemberId}/attendance?year=2026");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberAttendanceSummaryDto>>();
        Assert.True(body!.Success);
        Assert.Equal(factory.MemberId, body.Data!.MemberId);
        Assert.Contains(body.Data.PerMonth, m => m is { Year: 2026, Month: 5 });
        Assert.Contains(body.Data.Trainings, t => t.TrainingId == training.Id && t.Performance == 7);
        Assert.True(body.Data.AttendancePercentage > 0);
    }

    [Fact]
    public async Task MemberAttendance_ForAnotherMember_AsMember_Returns403()
    {
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await member.GetAsync($"/api/trainings/member/{factory.OtherMemberId}/attendance");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberAttendanceSummaryDto>>();
        Assert.False(body!.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task MemberAttendance_ForAMemberOfTheirClub_AsCoach_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync($"/api/trainings/member/{factory.OtherMemberId}/attendance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberAttendanceSummaryDto>>();
        Assert.True(body!.Success);
        Assert.Equal(factory.OtherMemberId, body.Data!.MemberId);
    }

    [Fact]
    public async Task MemberAttendance_ForAnUnknownMember_AsCoach_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/trainings/member/not-a-real-user-id/attendance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
