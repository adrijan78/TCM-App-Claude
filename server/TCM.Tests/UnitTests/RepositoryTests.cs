using Microsoft.EntityFrameworkCore;
using TCM.Application.Dtos.Members;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using TCM.Infrastructure.Repositories;

namespace TCM.Tests.UnitTests;

/// <summary>
/// The repository layer against a real provider. Two things are under test here that a
/// substitute cannot show: that each query actually translates to SQL, and that the club and
/// ownership filters are applied <em>in the query</em> rather than after materialising.
/// </summary>
public class RepositoryTests(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
{
    private static readonly DateOnly Today = new(2026, 8, 24);

    // ---- Members -----------------------------------------------------------------------------

    [Fact]
    public async Task Members_Search_ReturnsOnlyTheGivenClub()
    {
        var repo = new MemberRepository(fixture.Context);

        var results = await repo.SearchAsync(fixture.ClubId, new MemberFilterDto(null, null, null), Today);

        Assert.NotEmpty(results);
        Assert.DoesNotContain(results, m => m.Id == fixture.OutsiderId);
    }

    [Fact]
    public async Task Members_Search_MatchesOnNameAndEmail()
    {
        var repo = new MemberRepository(fixture.Context);

        var byName = await repo.SearchAsync(fixture.ClubId, new MemberFilterDto("Ana", null, null), Today);
        var byEmail = await repo.SearchAsync(fixture.ClubId, new MemberFilterDto("member@test", null, null), Today);

        Assert.Contains(byName, m => m.Id == fixture.MemberId);
        Assert.Contains(byEmail, m => m.Id == fixture.MemberId);
    }

    [Fact]
    public async Task Members_Search_FiltersByCurrentBelt()
    {
        // The member holds White historically and Black currently; the filter is about the
        // belt they hold now, so White must not match them.
        var repo = new MemberRepository(fixture.Context);

        var black = await repo.SearchAsync(fixture.ClubId, new MemberFilterDto(null, fixture.BlackBeltId, null), Today);
        var white = await repo.SearchAsync(fixture.ClubId, new MemberFilterDto(null, fixture.WhiteBeltId, null), Today);

        Assert.Contains(black, m => m.Id == fixture.MemberId);
        Assert.DoesNotContain(white, m => m.Id == fixture.MemberId);
        Assert.Contains(white, m => m.Id == fixture.PeerId);
    }

    [Fact]
    public async Task Members_Search_FiltersByAgeGroup()
    {
        // The peer was born in 2014, so they are 12 in 2026 — a Cadet, not a Kid.
        var repo = new MemberRepository(fixture.Context);

        var cadets = await repo.SearchAsync(fixture.ClubId, new MemberFilterDto(null, null, AgeGroup.Cadets), Today);
        var seniors = await repo.SearchAsync(fixture.ClubId, new MemberFilterDto(null, null, AgeGroup.Seniors), Today);

        Assert.Contains(cadets, m => m.Id == fixture.PeerId);
        Assert.DoesNotContain(cadets, m => m.Id == fixture.MemberId);
        Assert.Contains(seniors, m => m.Id == fixture.MemberId);
    }

    [Fact]
    public async Task Members_GetMember_ProjectsTheCurrentBelt()
    {
        var repo = new MemberRepository(fixture.Context);

        var member = await repo.GetMemberAsync(fixture.MemberId, Today);

        Assert.NotNull(member);
        Assert.Equal("Black", member!.CurrentBelt?.BeltName);
    }

    [Fact]
    public async Task Members_GetMember_ForAnUnknownId_IsNull()
    {
        var repo = new MemberRepository(fixture.Context);

        Assert.Null(await repo.GetMemberAsync("no-such-id", Today));
    }

    [Fact]
    public async Task Members_BeltHistory_IsNewestFirst()
    {
        var repo = new MemberRepository(fixture.Context);

        var history = await repo.GetBeltHistoryAsync(fixture.MemberId);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].DateReceived >= history[1].DateReceived);
    }

    // ---- Notes -------------------------------------------------------------------------------

    [Fact]
    public async Task Notes_ForClub_ExcludesOtherClubs()
    {
        var repo = new NoteRepository(fixture.Context);

        var notes = await repo.GetForClubAsync(fixture.ClubId, null);

        Assert.NotEmpty(notes);
        Assert.DoesNotContain(notes, n => n.Title == "Rival note");
    }

    [Fact]
    public async Task Notes_ForMember_PutHighPriorityFirst()
    {
        // SPEC section 6.8's ordering, applied in SQL rather than by the screen.
        var repo = new NoteRepository(fixture.Context);

        var notes = await repo.GetForMemberAsync(fixture.MemberId, null);

        Assert.Equal(NotePriority.High, notes[0].Priority);
    }

    [Fact]
    public async Task Notes_SearchMatchesTheTitle()
    {
        var repo = new NoteRepository(fixture.Context);

        var notes = await repo.GetForMemberAsync(fixture.MemberId, "Excellent");

        Assert.Single(notes);
        Assert.Equal("Excellent form", notes[0].Title);
    }

    [Fact]
    public async Task Notes_ForTrainingAndMember_ReturnsOnlyThatSessionsNotes()
    {
        var repo = new NoteRepository(fixture.Context);

        var notes = await repo.GetForTrainingAndMemberAsync(fixture.TrainingId, fixture.MemberId, null);

        Assert.Single(notes);
        Assert.Equal(fixture.TrainingId, notes[0].TrainingId);
    }

    [Fact]
    public async Task Notes_GetSubject_CarriesAuthorshipForTheDeleteRule()
    {
        var repo = new NoteRepository(fixture.Context);
        var note = await fixture.Context.Notes.AsNoTracking()
            .FirstAsync(n => n.Title == "Excellent form");

        var subject = await repo.GetSubjectAsync(note.Id);

        Assert.NotNull(subject);
        Assert.Equal(fixture.CoachId, subject!.FromMemberId);
        Assert.Equal(fixture.MemberId, subject.ToMemberId);
        Assert.Equal(fixture.ClubId, subject.ToMemberClubId);
    }

    [Fact]
    public async Task Notes_TrainingBelongsToClub_RefusesAnotherClubsSession()
    {
        var repo = new NoteRepository(fixture.Context);

        Assert.True(await repo.TrainingBelongsToClubAsync(fixture.TrainingId, fixture.ClubId));
        Assert.False(await repo.TrainingBelongsToClubAsync(fixture.OtherClubTrainingId, fixture.ClubId));
    }

    // ---- Trainings ---------------------------------------------------------------------------

    [Fact]
    public async Task Trainings_ForClub_ExcludesOtherClubs()
    {
        var repo = new TrainingRepository(fixture.Context);

        var list = await repo.GetForClubAsync(fixture.ClubId, null, null, null);

        Assert.NotEmpty(list);
        Assert.DoesNotContain(list, t => t.Description == "Rival session");
    }

    [Fact]
    public async Task Trainings_ForClub_AppliesTitleStatusAndTypeFilters()
    {
        var repo = new TrainingRepository(fixture.Context);

        var byTitle = await repo.GetForClubAsync(fixture.ClubId, "Sparring", null, null);
        var byStatus = await repo.GetForClubAsync(fixture.ClubId, null, TrainingStatus.Finished, null);
        var byType = await repo.GetForClubAsync(fixture.ClubId, null, null, TrainingType.Sparring);

        Assert.Single(byTitle);
        Assert.Single(byStatus);
        Assert.Single(byType);
        Assert.Equal("Belt exam preparation", byType[0].Description);
    }

    [Fact]
    public async Task Trainings_ForClub_CountsInvitedAndPresent()
    {
        var repo = new TrainingRepository(fixture.Context);

        var training = (await repo.GetForClubAsync(fixture.ClubId, "Sparring", null, null)).Single();

        Assert.Equal(2, training.InvitedCount);
        Assert.Equal(1, training.PresentCount);
    }

    [Fact]
    public async Task Trainings_Calendar_NarrowsToTheMonth()
    {
        var repo = new TrainingRepository(fixture.Context);

        var march = await repo.GetCalendarAsync(fixture.ClubId, 2026, 3);
        var april = await repo.GetCalendarAsync(fixture.ClubId, 2026, 4);

        Assert.Single(march);
        Assert.Single(april);
        Assert.Equal("Sparring drills", march[0].Description);
    }

    [Fact]
    public async Task Trainings_IsInvited_IsTheMemberPermission()
    {
        var repo = new TrainingRepository(fixture.Context);

        Assert.True(await repo.IsInvitedAsync(fixture.TrainingId, fixture.MemberId));
        Assert.False(await repo.IsInvitedAsync(fixture.TrainingId, fixture.OutsiderId));
    }

    [Fact]
    public async Task Trainings_GetClubId_IsNullForAMissingTraining()
    {
        var repo = new TrainingRepository(fixture.Context);

        Assert.Equal(fixture.ClubId, await repo.GetClubIdAsync(fixture.TrainingId));
        Assert.Null(await repo.GetClubIdAsync(987654));
    }

    [Fact]
    public async Task Trainings_GetClubMembers_DropsOutsidersAndInactiveMembers()
    {
        // Whatever the caller sent, only active members of this club come back — which is what
        // lets the service compare counts and refuse the request.
        var repo = new TrainingRepository(fixture.Context);
        var inactiveId = await fixture.Context.Users
            .Where(u => u.Email == "gone@test.local").Select(u => u.Id).SingleAsync();

        var invitees = await repo.GetClubMembersAsync(
            fixture.ClubId, [fixture.MemberId, fixture.OutsiderId, inactiveId]);

        Assert.Single(invitees);
        Assert.Equal(fixture.MemberId, invitees[0].MemberId);
    }

    [Fact]
    public async Task Trainings_MemberAttendance_CountsWhatTheChartsRead()
    {
        var repo = new TrainingRepository(fixture.Context);

        var summary = await repo.GetMemberAttendanceAsync(fixture.MemberId, null);

        // Two invitations, one of them attended.
        Assert.Equal(2, summary.InvitedCount);
        Assert.Equal(1, summary.PresentCount);
    }

    [Fact]
    public async Task Trainings_Details_ListsEveryInvitee()
    {
        var repo = new TrainingRepository(fixture.Context);

        var details = await repo.GetDetailsAsync(fixture.TrainingId);

        Assert.NotNull(details);
        Assert.Equal(2, details!.Attendees.Count);
        Assert.Contains(details.Attendees, a => a.MemberId == fixture.MemberId && a.Performance == 8);
    }

    // ---- Payments ----------------------------------------------------------------------------

    [Fact]
    public async Task Payments_ClubHistory_ExcludesOtherClubs()
    {
        var repo = new PaymentRepository(fixture.Context);

        var payments = await repo.GetClubHistoryAsync(fixture.ClubId, null, null, null, null);

        Assert.All(payments, p => Assert.NotEqual(fixture.OutsiderId, p.MemberId));
    }

    [Fact]
    public async Task Payments_ClubHistory_FiltersByYearMonthMemberAndMethod()
    {
        var repo = new PaymentRepository(fixture.Context);

        var january = await repo.GetClubHistoryAsync(fixture.ClubId, 2026, 1, null, null);
        var online = await repo.GetClubHistoryAsync(fixture.ClubId, null, null, null, true);
        var cash = await repo.GetClubHistoryAsync(fixture.ClubId, null, null, null, false);
        var byMember = await repo.GetClubHistoryAsync(fixture.ClubId, null, null, fixture.PeerId, null);

        Assert.Single(january);
        Assert.Single(online);
        Assert.Single(cash);
        Assert.Empty(byMember);
    }

    [Fact]
    public async Task Payments_GetByStripeSessionId_IsTheIdempotencyLookup()
    {
        var repo = new PaymentRepository(fixture.Context);

        Assert.NotNull(await repo.GetByStripeSessionIdAsync("sess_seeded"));
        Assert.Null(await repo.GetByStripeSessionIdAsync("sess_never_used"));
    }

    [Fact]
    public async Task Payments_AddIfSessionUnused_RefusesASecondRowForTheSameSession()
    {
        // The replay guard, exercised against the real unique index rather than a mock.
        var repo = new PaymentRepository(fixture.Context);

        var (added, stored) = await repo.AddIfSessionUnusedAsync(new Payment
        {
            MemberId = fixture.MemberId,
            IsPaidOnline = true,
            PaymentDate = DateTime.UtcNow,
            NextPaymentDate = new DateOnly(2026, 12, 1),
            StripeSessionId = "sess_seeded"
        });

        Assert.False(added);
        Assert.Equal("sess_seeded", stored.StripeSessionId);
        Assert.Equal(1, await fixture.Context.Payments.CountAsync(p => p.StripeSessionId == "sess_seeded"));
    }

    [Fact]
    public async Task Payments_LatestNextPaymentDate_IsWhatARenewalExtendsFrom()
    {
        var repo = new PaymentRepository(fixture.Context);

        var latest = await repo.GetLatestNextPaymentDateAsync(fixture.MemberId);

        Assert.Equal(new DateOnly(2026, 3, 8), latest);
    }

    [Fact]
    public async Task Payments_FindInClub_RefusesAPaymentFromAnotherClub()
    {
        // Null means "not there, or not yours" — the service is not allowed to tell them apart.
        var repo = new PaymentRepository(fixture.Context);
        var outsiderPayment = await fixture.Context.Payments.AsNoTracking()
            .FirstAsync(p => p.MemberId == fixture.OutsiderId);

        Assert.Null(await repo.FindInClubAsync(outsiderPayment.Id, fixture.ClubId));
        Assert.NotNull(await repo.FindInClubAsync(outsiderPayment.Id, fixture.OtherClubId));
    }

    [Fact]
    public async Task Payments_MemberHistory_IsNewestFirst()
    {
        var repo = new PaymentRepository(fixture.Context);

        var history = await repo.GetMemberHistoryAsync(fixture.MemberId);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].PaymentDate >= history[1].PaymentDate);
    }
}
