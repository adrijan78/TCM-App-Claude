using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Notes;
using TCM.Application.Services;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using static TCM.Tests.UnitTests.TestDoubles;

namespace TCM.Tests.UnitTests;

/// <summary>
/// The two rules that shape <see cref="NoteService"/>, tested directly: the author of a note is
/// always the caller's token id, and "delete only own notes" (SPEC section 5) is measured by
/// authorship, not by who the note is about.
/// </summary>
public class NoteServiceTests
{
    private readonly INoteRepository _notes = Substitute.For<INoteRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    private NoteService Build(params ApplicationUser[] users) => new(
        _notes,
        UserManagerFor(users),
        _email,
        PassingValidator<CreateNoteDto>(),
        ClientSettings(),
        Logger<NoteService>());

    // ---- Club notes --------------------------------------------------------------------------

    [Fact]
    public async Task GetClubNotes_AsMember_IsForbiddenEvenWithoutTheAttribute()
    {
        // The controller carries [Authorize(Roles = Coach)]. This proves the rule survives a
        // caller that does not.
        var member = User(isCoach: false);
        var service = Build(member);

        var result = await service.GetClubNotesAsync(member.Id, isCoach: false, search: null);

        Assert.False(result.Success);
        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _notes.DidNotReceiveWithAnyArgs().GetForClubAsync(default, default);
    }

    [Fact]
    public async Task GetClubNotes_UsesTheCoachsOwnClub_NotAnythingFromTheRequest()
    {
        var coach = User(clubId: 7, isCoach: true);
        _notes.GetForClubAsync(7, null, Arg.Any<CancellationToken>()).Returns([]);
        var service = Build(coach);

        var result = await service.GetClubNotesAsync(coach.Id, isCoach: true, search: null);

        Assert.True(result.Success);
        await _notes.Received(1).GetForClubAsync(7, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetClubNotes_AsCoachWithNoClub_IsForbidden()
    {
        var coach = User(clubId: null, isCoach: true);
        var service = Build(coach);

        var result = await service.GetClubNotesAsync(coach.Id, isCoach: true, search: null);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    // ---- Reading one member's notes ----------------------------------------------------------

    [Fact]
    public async Task GetForMember_AsMemberReachingForAnotherId_IsForbidden()
    {
        var member = User();
        var stranger = User();
        var service = Build(member, stranger);

        var result = await service.GetForMemberAsync(stranger.Id, member.Id, isCoach: false, search: null);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _notes.DidNotReceiveWithAnyArgs().GetForMemberAsync(default!, default);
    }

    [Fact]
    public async Task GetForMember_AsMemberReadingTheirOwn_IsAllowed()
    {
        var member = User();
        _notes.GetForMemberAsync(member.Id, null, Arg.Any<CancellationToken>()).Returns([]);
        var service = Build(member);

        var result = await service.GetForMemberAsync(member.Id, member.Id, isCoach: false, search: null);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetForMember_AsCoachFromAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        var outsider = User(clubId: 2);
        var service = Build(coach, outsider);

        var result = await service.GetForMemberAsync(outsider.Id, coach.Id, isCoach: true, search: null);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task GetForTraining_WithoutATraining_Fails()
    {
        var member = User();
        var service = Build(member);

        var result = await service.GetForTrainingAsync(0, member.Id, member.Id, isCoach: false, search: null);

        Assert.False(result.Success);
        Assert.Equal(ErrorKind.Validation, result.ErrorKind);
    }

    // ---- Creating ----------------------------------------------------------------------------

    [Fact]
    public async Task Create_AsMemberAboutSomeoneElse_IsForbidden()
    {
        var member = User();
        var subject = User();
        var service = Build(member, subject);

        var result = await service.CreateAsync(
            new CreateNoteDto("T", "C", NotePriority.Low, subject.Id, null), member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _notes.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_AcrossAClubBoundary_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        var outsider = User(clubId: 2);
        var service = Build(coach, outsider);

        var result = await service.CreateAsync(
            new CreateNoteDto("T", "C", NotePriority.High, outsider.Id, null), coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _notes.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_TakesTheAuthorFromTheToken()
    {
        var coach = User(clubId: 1, isCoach: true);
        var subject = User(clubId: 1);
        Note? saved = null;
        await _notes.AddAsync(Arg.Do<Note>(n => saved = n), Arg.Any<CancellationToken>());
        _notes.GetDtoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new NoteDto(1, "T", "C", DateTime.UtcNow, NotePriority.High,
                coach.Id, "Test Coach", subject.Id, "Test Person", null, null));
        var service = Build(coach, subject);

        var result = await service.CreateAsync(
            new CreateNoteDto("T", "C", NotePriority.High, subject.Id, null), coach.Id, isCoach: true);

        Assert.True(result.Success);
        Assert.NotNull(saved);
        Assert.Equal(coach.Id, saved!.FromMemberId);
        Assert.Equal(subject.Id, saved.ToMemberId);
    }

    [Fact]
    public async Task Create_AboutAnUnknownMember_IsNotFound()
    {
        var coach = User(isCoach: true);
        var service = Build(coach);

        var result = await service.CreateAsync(
            new CreateNoteDto("T", "C", NotePriority.Low, "no-such-id", null), coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task Create_EmailsTheSubject()
    {
        var coach = User(clubId: 1, isCoach: true);
        var subject = User(clubId: 1, email: "subject@test.local");
        _notes.GetDtoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new NoteDto(1, "T", "C", DateTime.UtcNow, NotePriority.High,
                coach.Id, "Test Coach", subject.Id, "Test Person", null, null));
        var service = Build(coach, subject);

        await service.CreateAsync(
            new CreateNoteDto("T", "C", NotePriority.High, subject.Id, null), coach.Id, isCoach: true);

        await _email.Received(1).SendAsync(
            Arg.Is<SendEmailRequest>(r => r.ToEmail == "subject@test.local"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AboutYourself_SendsNoEmail()
    {
        var member = User(clubId: 1);
        _notes.GetDtoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new NoteDto(1, "T", "C", DateTime.UtcNow, NotePriority.Low,
                member.Id, "Test Person", member.Id, "Test Person", null, null));
        var service = Build(member);

        await service.CreateAsync(
            new CreateNoteDto("T", "C", NotePriority.Low, member.Id, null), member.Id, isCoach: false);

        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Create_SurvivesAnEmailServiceThatThrows()
    {
        // The note is committed before the email is attempted; a throwing sender must not
        // turn a saved note into a failed request.
        var coach = User(clubId: 1, isCoach: true);
        var subject = User(clubId: 1, email: "subject@test.local");
        _email.SendAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP is down"));
        _notes.GetDtoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new NoteDto(1, "T", "C", DateTime.UtcNow, NotePriority.High,
                coach.Id, "Test Coach", subject.Id, "Test Person", null, null));
        var service = Build(coach, subject);

        var result = await service.CreateAsync(
            new CreateNoteDto("T", "C", NotePriority.High, subject.Id, null), coach.Id, isCoach: true);

        Assert.True(result.Success);
        await _notes.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithAnInvalidDto_StopsBeforeTouchingTheRepository()
    {
        var coach = User(isCoach: true);
        var service = new NoteService(
            _notes,
            UserManagerFor(coach),
            _email,
            FailingValidator<CreateNoteDto>(nameof(CreateNoteDto.Title), "Title is required."),
            ClientSettings(),
            Logger<NoteService>());

        var result = await service.CreateAsync(
            new CreateNoteDto("", "C", NotePriority.Low, coach.Id, null), coach.Id, isCoach: true);

        Assert.False(result.Success);
        Assert.Contains("Title is required.", result.Errors);
        await _notes.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // ---- Deleting: SPEC section 5's authorship rule -------------------------------------------

    [Fact]
    public async Task Delete_AsMemberOfANoteTheyDidNotWrite_IsForbidden()
    {
        // The member is the *subject* of a coach's note. Being written about is not ownership.
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _notes.GetSubjectAsync(5, Arg.Any<CancellationToken>())
            .Returns(new NoteSubject(5, coach.Id, member.Id, 1));
        var service = Build(coach, member);

        var result = await service.DeleteAsync(5, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        _notes.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task Delete_AsMemberOfTheirOwnNote_IsAllowed()
    {
        var member = User(clubId: 1);
        var note = new Note { Id = 5, Title = "T", Content = "C", FromMemberId = member.Id, ToMemberId = member.Id };
        _notes.GetSubjectAsync(5, Arg.Any<CancellationToken>())
            .Returns(new NoteSubject(5, member.Id, member.Id, 1));
        _notes.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(note);
        var service = Build(member);

        var result = await service.DeleteAsync(5, member.Id, isCoach: false);

        Assert.True(result.Success);
        _notes.Received(1).Remove(note);
        await _notes.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_OfAMissingNote_TellsACoachAndNotAMember()
    {
        // Identical answers would let a member enumerate note ids by status code.
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _notes.GetSubjectAsync(404, Arg.Any<CancellationToken>()).Returns((NoteSubject?)null);
        var service = Build(coach, member);

        var asCoach = await service.DeleteAsync(404, coach.Id, isCoach: true);
        var asMember = await service.DeleteAsync(404, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.NotFound, asCoach.ErrorKind);
        Assert.Equal(ErrorKind.Forbidden, asMember.ErrorKind);
    }

    [Fact]
    public async Task Delete_AsCoachFromAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        _notes.GetSubjectAsync(5, Arg.Any<CancellationToken>())
            .Returns(new NoteSubject(5, "someone", "someone-else", 2));
        var service = Build(coach);

        var result = await service.DeleteAsync(5, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        _notes.DidNotReceiveWithAnyArgs().Remove(default!);
    }
}
