using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Trainings;
using TCM.Application.Services;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using static TCM.Tests.UnitTests.TestDoubles;

namespace TCM.Tests.UnitTests;

/// <summary>
/// <see cref="TrainingService"/>: who may see a session, who may report attendance for whom, and
/// the invitation emails that go out when one is created (SPEC sections 6.5 and 6.6).
/// </summary>
public class TrainingServiceTests
{
    private readonly ITrainingRepository _trainings = Substitute.For<ITrainingRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    private TrainingService Build(params ApplicationUser[] users) => new(
        _trainings,
        UserManagerFor(users),
        _email,
        PassingValidator<EditTrainingDto>(),
        PassingValidator<ReportAttendanceDto>(),
        PassingValidator<SetPerformanceDto>(),
        ClientSettings(),
        Logger<TrainingService>());

    private static TrainingDetailsDto DetailsWith(params TrainingAttendeeDto[] attendees) =>
        new(1, DateTime.UtcNow, "Session", TrainingType.Regular, TrainingStatus.Active, attendees);

    // ---- Listing -----------------------------------------------------------------------------

    [Fact]
    public async Task GetTrainings_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.GetTrainingsAsync(member.Id, null, null, null);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _trainings.DidNotReceiveWithAnyArgs().GetForClubAsync(default, default, default, default);
    }

    [Fact]
    public async Task GetTrainings_ScopesToTheCoachsOwnClub()
    {
        var coach = User(clubId: 3, isCoach: true);
        _trainings.GetForClubAsync(3, null, null, null, Arg.Any<CancellationToken>()).Returns([]);
        var service = Build(coach);

        var result = await service.GetTrainingsAsync(coach.Id, null, null, null);

        Assert.True(result.Success);
        await _trainings.Received(1).GetForClubAsync(3, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCalendar_WithAnImpossibleMonth_IsRejected()
    {
        var coach = User(isCoach: true);
        var service = Build(coach);

        var result = await service.GetCalendarAsync(coach.Id, 2026, 13);

        Assert.False(result.Success);
    }

    // ---- Reading one training ----------------------------------------------------------------

    [Fact]
    public async Task GetDetails_AsAnUninvitedMember_IsForbidden()
    {
        // SPEC section 6.6: the invitation is the whole permission.
        var member = User(clubId: 1);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.IsInvitedAsync(1, member.Id, Arg.Any<CancellationToken>()).Returns(false);
        var service = Build(member);

        var result = await service.GetDetailsAsync(1, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _trainings.DidNotReceiveWithAnyArgs().GetDetailsAsync(default, default);
    }

    [Fact]
    public async Task GetDetails_OfAMissingTraining_TellsACoachAndNotAMember()
    {
        var coach = User(isCoach: true);
        var member = User();
        _trainings.GetClubIdAsync(404, Arg.Any<CancellationToken>()).Returns((int?)null);
        var service = Build(coach, member);

        var asCoach = await service.GetDetailsAsync(404, coach.Id, isCoach: true);
        var asMember = await service.GetDetailsAsync(404, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.NotFound, asCoach.ErrorKind);
        Assert.Equal(ErrorKind.Forbidden, asMember.ErrorKind);
    }

    [Fact]
    public async Task GetDetails_AsCoachFromAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(2);
        var service = Build(coach);

        var result = await service.GetDetailsAsync(1, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task GetDetails_AsAnInvitedMember_HidesPeerScoresAndAbsenceReasons()
    {
        // An absence reason is free text about someone who may be a minor, and SPEC section 5
        // gives a member "views own only" for performance.
        var member = User(clubId: 1);
        var peer = User(clubId: 1);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.IsInvitedAsync(1, member.Id, Arg.Any<CancellationToken>()).Returns(true);
        _trainings.GetDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(DetailsWith(
            new TrainingAttendeeDto(member.Id, "Test", "Member", AttendanceStatus.Present, null, 8),
            new TrainingAttendeeDto(peer.Id, "Peer", "Person", AttendanceStatus.Absent, "Hospital appointment", 5)));
        var service = Build(member, peer);

        var result = await service.GetDetailsAsync(1, member.Id, isCoach: false);

        Assert.True(result.Success);
        var own = result.Data!.Attendees.Single(a => a.MemberId == member.Id);
        var other = result.Data.Attendees.Single(a => a.MemberId == peer.Id);

        Assert.Equal(8, own.Performance);
        Assert.Null(other.Performance);
        Assert.Null(other.AbsenceReason);

        // They can still see that the peer was invited and whether they turned up.
        Assert.Equal(AttendanceStatus.Absent, other.Status);
    }

    [Fact]
    public async Task GetDetails_AsCoach_ShowsEveryScore()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.GetDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(DetailsWith(
            new TrainingAttendeeDto(member.Id, "Test", "Member", AttendanceStatus.Absent, "Injured", 7)));
        var service = Build(coach, member);

        var result = await service.GetDetailsAsync(1, coach.Id, isCoach: true);

        Assert.Equal(7, result.Data!.Attendees.Single().Performance);
        Assert.Equal("Injured", result.Data.Attendees.Single().AbsenceReason);
    }

    // ---- Creating ----------------------------------------------------------------------------

    [Fact]
    public async Task Create_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.CreateAsync(
            new EditTrainingDto("S", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active, []),
            member.Id);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _trainings.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_WithAnInviteeOutsideTheClub_IsRejectedRatherThanSilentlyDropped()
    {
        var coach = User(clubId: 1, isCoach: true);
        // Two ids requested, one resolvable — the outsider is not an active member of this club.
        _trainings.GetClubMembersAsync(1, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new TrainingInviteeDto("in-club", "In", "Club", "in@test.local")]);
        var service = Build(coach);

        var result = await service.CreateAsync(
            new EditTrainingDto("S", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active,
                ["in-club", "outsider"]),
            coach.Id);

        Assert.False(result.Success);
        await _trainings.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_InvitesEveryoneAsInvitedAndEmailsThem()
    {
        var coach = User(clubId: 1, isCoach: true);
        _trainings.GetClubMembersAsync(1, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([
                new TrainingInviteeDto("m1", "Ana", "One", "ana@test.local"),
                new TrainingInviteeDto("m2", "Bo", "Two", "bo@test.local")
            ]);
        Training? saved = null;
        await _trainings.AddAsync(Arg.Do<Training>(t => saved = t), Arg.Any<CancellationToken>());
        _trainings.GetDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(DetailsWith());
        var service = Build(coach);

        var result = await service.CreateAsync(
            new EditTrainingDto("Sparring", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active,
                ["m1", "m2"]),
            coach.Id);

        Assert.True(result.Success);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Attendances.Count);
        Assert.All(saved.Attendances, a => Assert.Equal(AttendanceStatus.Invited, a.Status));

        await _email.Received(1).SendAsync(
            Arg.Is<SendEmailRequest>(r => r.ToEmail == "ana@test.local"), Arg.Any<CancellationToken>());
        await _email.Received(1).SendAsync(
            Arg.Is<SendEmailRequest>(r => r.ToEmail == "bo@test.local"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_SurvivesAnInvitationEmailThatFails()
    {
        // The training is already committed. A dead SMTP server must not lose it.
        var coach = User(clubId: 1, isCoach: true);
        _trainings.GetClubMembersAsync(1, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new TrainingInviteeDto("m1", "Ana", "One", "ana@test.local")]);
        _email.SendAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP is down"));
        _trainings.GetDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(DetailsWith());
        var service = Build(coach);

        var result = await service.CreateAsync(
            new EditTrainingDto("Sparring", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active, ["m1"]),
            coach.Id);

        Assert.True(result.Success);
        await _trainings.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithAnInviteeWhoHasNoEmail_StillCreatesTheTraining()
    {
        var coach = User(clubId: 1, isCoach: true);
        _trainings.GetClubMembersAsync(1, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new TrainingInviteeDto("m1", "Ana", "One", null)]);
        _trainings.GetDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(DetailsWith());
        var service = Build(coach);

        var result = await service.CreateAsync(
            new EditTrainingDto("Sparring", DateTime.UtcNow, TrainingType.Regular, TrainingStatus.Active, ["m1"]),
            coach.Id);

        Assert.True(result.Success);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    // ---- Reporting attendance ----------------------------------------------------------------

    [Fact]
    public async Task ReportAttendance_AsAMemberForSomeoneElse_IsForbidden()
    {
        var member = User(clubId: 1);
        var peer = User(clubId: 1);
        var service = Build(member, peer);

        var result = await service.ReportAttendanceAsync(
            1, new ReportAttendanceDto(peer.Id, AttendanceStatus.Present, null), member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _trainings.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ReportAttendance_AsAMemberWithNoMemberId_MeansThemselves()
    {
        var member = User(clubId: 1);
        var attendance = new Attendance { MemberId = member.Id, TrainingId = 1, Status = AttendanceStatus.Invited };
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.GetAttendanceAsync(1, member.Id, Arg.Any<CancellationToken>()).Returns(attendance);
        var service = Build(member);

        var result = await service.ReportAttendanceAsync(
            1, new ReportAttendanceDto(null, AttendanceStatus.Present, null), member.Id, isCoach: false);

        Assert.True(result.Success);
        Assert.Equal(AttendanceStatus.Present, attendance.Status);
    }

    [Fact]
    public async Task ReportAttendance_AsAnUninvitedMember_IsForbidden()
    {
        var member = User(clubId: 1);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.GetAttendanceAsync(1, member.Id, Arg.Any<CancellationToken>()).Returns((Attendance?)null);
        var service = Build(member);

        var result = await service.ReportAttendanceAsync(
            1, new ReportAttendanceDto(null, AttendanceStatus.Present, null), member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task ReportAttendance_MarkingPresent_ClearsAStaleAbsenceReason()
    {
        var member = User(clubId: 1);
        var attendance = new Attendance
        {
            MemberId = member.Id,
            TrainingId = 1,
            Status = AttendanceStatus.Absent,
            Description = "Injured"
        };
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.GetAttendanceAsync(1, member.Id, Arg.Any<CancellationToken>()).Returns(attendance);
        var service = Build(member);

        await service.ReportAttendanceAsync(
            1, new ReportAttendanceDto(null, AttendanceStatus.Present, null), member.Id, isCoach: false);

        Assert.Null(attendance.Description);
    }

    [Fact]
    public async Task ReportAttendance_MarkingAbsent_KeepsTheReason()
    {
        var member = User(clubId: 1);
        var attendance = new Attendance { MemberId = member.Id, TrainingId = 1, Status = AttendanceStatus.Invited };
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.GetAttendanceAsync(1, member.Id, Arg.Any<CancellationToken>()).Returns(attendance);
        var service = Build(member);

        await service.ReportAttendanceAsync(
            1, new ReportAttendanceDto(null, AttendanceStatus.Absent, "  Hospital  "), member.Id, isCoach: false);

        Assert.Equal("Hospital", attendance.Description);
    }

    [Fact]
    public async Task ReportAttendance_AsCoachForAnotherClubsTraining_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(2);
        var service = Build(coach);

        var result = await service.ReportAttendanceAsync(
            1, new ReportAttendanceDto("someone", AttendanceStatus.Present, null), coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task ReportAttendance_WithACoachRoleButANonCoachAccount_CannotActForOthers()
    {
        // isCoach comes from the token; caller.IsCoach comes from the row. The service trusts
        // only the pair, so a stale role claim is not enough.
        var notReallyACoach = User(clubId: 1, isCoach: false);
        var peer = User(clubId: 1);
        var service = Build(notReallyACoach, peer);

        var result = await service.ReportAttendanceAsync(
            1, new ReportAttendanceDto(peer.Id, AttendanceStatus.Present, null),
            notReallyACoach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    // ---- Scoring -----------------------------------------------------------------------------

    [Fact]
    public async Task SetPerformance_ForAMemberWhoWasNotInvited_IsRejected()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _trainings.GetAttendanceAsync(1, member.Id, Arg.Any<CancellationToken>()).Returns((Attendance?)null);
        var service = Build(coach, member);

        var result = await service.SetPerformanceAsync(1, member.Id, new SetPerformanceDto(8), coach.Id);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SetPerformance_AsCoachFromAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        _trainings.GetClubIdAsync(1, Arg.Any<CancellationToken>()).Returns(2);
        var service = Build(coach);

        var result = await service.SetPerformanceAsync(1, "anyone", new SetPerformanceDto(8), coach.Id);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    // ---- A member's attendance summary --------------------------------------------------------

    [Fact]
    public async Task GetMemberAttendance_AsMemberForAnotherId_IsForbidden()
    {
        var member = User(clubId: 1);
        var peer = User(clubId: 1);
        var service = Build(member, peer);

        var result = await service.GetMemberAttendanceAsync(peer.Id, null, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _trainings.DidNotReceiveWithAnyArgs().GetMemberAttendanceAsync(default!, default);
    }

    [Fact]
    public async Task GetMemberAttendance_AsCoachFromAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        var outsider = User(clubId: 2);
        var service = Build(coach, outsider);

        var result = await service.GetMemberAttendanceAsync(outsider.Id, null, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }
}
