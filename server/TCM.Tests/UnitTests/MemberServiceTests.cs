using NSubstitute;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Common;
using TCM.Application.Dtos.Members;
using TCM.Application.Services;
using TCM.Domain.Entities;
using static TCM.Tests.UnitTests.TestDoubles;

namespace TCM.Tests.UnitTests;

/// <summary>
/// <see cref="MemberService"/>'s authorization decisions and the belt-history invariants.
/// The club a query is scoped to always comes from the caller's own account — there is no club
/// parameter on any of these routes, and these tests are what keeps it that way.
/// </summary>
public class MemberServiceTests
{
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();

    private MemberService Build(params ApplicationUser[] users) => new(
        _members,
        UserManagerFor(users),
        PassingValidator<EditMemberDto>(),
        PassingValidator<AddMemberBeltDto>(),
        Logger<MemberService>());

    private static MemberFilterDto NoFilter => new(null, null, null);

    private static MemberDto DtoFor(ApplicationUser user) => new(
        user.Id, user.FirstName, user.LastName, user.Email!, null,
        user.DateOfBirth, 26, user.StartedOn, user.IsActive, user.IsCoach,
        null, null, null, null);

    // ---- The member list ---------------------------------------------------------------------

    [Fact]
    public async Task GetMembers_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.GetMembersAsync(NoFilter, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _members.DidNotReceiveWithAnyArgs().SearchAsync(default, default!, default);
    }

    [Fact]
    public async Task GetMembers_ScopesToTheCoachsOwnClub()
    {
        var coach = User(clubId: 42, isCoach: true);
        _members.SearchAsync(42, Arg.Any<MemberFilterDto>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var service = Build(coach);

        var result = await service.GetMembersAsync(NoFilter, coach.Id, isCoach: true);

        Assert.True(result.Success);
        await _members.Received(1).SearchAsync(
            42, Arg.Any<MemberFilterDto>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMembers_AsAClublessCoach_FailsClosed()
    {
        // Passing a null club id through would match every clubless user in the database.
        var coach = User(clubId: null, isCoach: true);
        var service = Build(coach);

        var result = await service.GetMembersAsync(NoFilter, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _members.DidNotReceiveWithAnyArgs().SearchAsync(default, default!, default);
    }

    [Fact]
    public async Task GetMembers_WithAnOverlongSearch_IsRejected()
    {
        var coach = User(isCoach: true);
        var service = Build(coach);

        var result = await service.GetMembersAsync(
            new MemberFilterDto(new string('x', 101), null, null), coach.Id, isCoach: true);

        Assert.False(result.Success);
        await _members.DidNotReceiveWithAnyArgs().SearchAsync(default, default!, default);
    }

    [Fact]
    public async Task GetMembers_WithANonPositiveBeltFilter_IsRejected()
    {
        var coach = User(isCoach: true);
        var service = Build(coach);

        var result = await service.GetMembersAsync(
            new MemberFilterDto(null, 0, null), coach.Id, isCoach: true);

        Assert.False(result.Success);
    }

    // ---- One member --------------------------------------------------------------------------

    [Fact]
    public async Task GetMember_AsMemberReachingForAnotherId_IsForbidden()
    {
        var member = User();
        var stranger = User();
        var service = Build(member, stranger);

        var result = await service.GetMemberAsync(stranger.Id, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task GetMember_AsMemberReadingTheirOwn_IsAllowed()
    {
        var member = User();
        _members.GetMemberAsync(member.Id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(DtoFor(member));
        var service = Build(member);

        var result = await service.GetMemberAsync(member.Id, member.Id, isCoach: false);

        Assert.True(result.Success);
        Assert.Equal(member.Id, result.Data!.Id);
    }

    [Fact]
    public async Task GetMember_AsCoachFromAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        var outsider = User(clubId: 2);
        var service = Build(coach, outsider);

        var result = await service.GetMemberAsync(outsider.Id, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task GetMember_ThatDoesNotExist_IsNotFoundForACoach()
    {
        var coach = User(isCoach: true);
        var service = Build(coach);

        var result = await service.GetMemberAsync("no-such-id", coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.NotFound, result.ErrorKind);
    }

    // ---- Deactivation ------------------------------------------------------------------------

    [Fact]
    public async Task Deactivate_AsMember_IsForbidden()
    {
        var member = User();
        var other = User();
        var service = Build(member, other);

        var result = await service.DeactivateAsync(other.Id, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task Deactivate_OfTheCoachsOwnAccount_IsRefused()
    {
        // 1 coach : 1 club, so nobody could reverse it and the coach could not sign back in.
        var coach = User(isCoach: true);
        var service = Build(coach);

        var result = await service.DeactivateAsync(coach.Id, coach.Id, isCoach: true);

        Assert.False(result.Success);
        Assert.True(coach.IsActive);
    }

    [Fact]
    public async Task Deactivate_FlagsTheRowAndNeverDeletesIt()
    {
        // SPEC section 6.3: attendance, payments, belts and notes all reference this row.
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.GetMemberAsync(member.Id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(DtoFor(member));
        var service = Build(coach, member);

        var result = await service.DeactivateAsync(member.Id, coach.Id, isCoach: true);

        Assert.True(result.Success);
        Assert.False(member.IsActive);
        _members.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task Deactivate_AcrossAClubBoundary_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        var outsider = User(clubId: 2);
        var service = Build(coach, outsider);

        var result = await service.DeactivateAsync(outsider.Id, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        Assert.True(outsider.IsActive);
    }

    // ---- Belt history ------------------------------------------------------------------------

    [Fact]
    public async Task AddBelt_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.AddBeltAsync(
            member.Id, new AddMemberBeltDto(1, new DateOnly(2026, 1, 1), null, true),
            member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _members.DidNotReceiveWithAnyArgs().AddBeltAsync(default!, default);
    }

    [Fact]
    public async Task AddBelt_WithAnUnknownBelt_IsRejected()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.BeltExistsAsync(99, Arg.Any<CancellationToken>()).Returns(false);
        var service = Build(coach, member);

        var result = await service.AddBeltAsync(
            member.Id, new AddMemberBeltDto(99, new DateOnly(2026, 1, 1), null, true),
            coach.Id, isCoach: true);

        Assert.False(result.Success);
        await _members.DidNotReceiveWithAnyArgs().AddBeltAsync(default!, default);
    }

    [Fact]
    public async Task AddBelt_TheFirstOne_IsCurrentWhateverTheFormSaid()
    {
        // Otherwise the profile would show a belt history with no current belt in it.
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.BeltExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        _members.CountBeltsAsync(member.Id, Arg.Any<CancellationToken>()).Returns(0);
        _members.AddBeltAsync(Arg.Any<MemberBelt>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var belt = call.Arg<MemberBelt>();
                belt.Id = 1;
                belt.Belt = new Belt { Id = 1, BeltName = "White", Rank = 1 };
                return belt;
            });
        var service = Build(coach, member);

        var result = await service.AddBeltAsync(
            member.Id, new AddMemberBeltDto(1, new DateOnly(2026, 1, 1), null, IsCurrentBelt: false),
            coach.Id, isCoach: true);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsCurrentBelt);
    }

    [Fact]
    public async Task AddBelt_ClearsTheOldCurrentBeltBeforeInserting()
    {
        // The unique filtered index allows one flagged row per member and SQL Server checks it
        // per statement, so the order of these two calls is the whole point.
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.BeltExistsAsync(2, Arg.Any<CancellationToken>()).Returns(true);
        _members.CountBeltsAsync(member.Id, Arg.Any<CancellationToken>()).Returns(3);
        _members.AddBeltAsync(Arg.Any<MemberBelt>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var belt = call.Arg<MemberBelt>();
                belt.Id = 4;
                belt.Belt = new Belt { Id = 2, BeltName = "Yellow", Rank = 2 };
                return belt;
            });
        var service = Build(coach, member);

        await service.AddBeltAsync(
            member.Id, new AddMemberBeltDto(2, new DateOnly(2026, 1, 1), null, IsCurrentBelt: true),
            coach.Id, isCoach: true);

        Received.InOrder(() =>
        {
            _members.ClearCurrentBeltAsync(member.Id, Arg.Any<CancellationToken>());
            _members.AddBeltAsync(Arg.Any<MemberBelt>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AddBelt_ThatIsNotCurrent_LeavesTheExistingCurrentBeltAlone()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.BeltExistsAsync(2, Arg.Any<CancellationToken>()).Returns(true);
        _members.CountBeltsAsync(member.Id, Arg.Any<CancellationToken>()).Returns(3);
        _members.AddBeltAsync(Arg.Any<MemberBelt>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var belt = call.Arg<MemberBelt>();
                belt.Id = 4;
                belt.Belt = new Belt { Id = 2, BeltName = "Yellow", Rank = 2 };
                return belt;
            });
        var service = Build(coach, member);

        await service.AddBeltAsync(
            member.Id, new AddMemberBeltDto(2, new DateOnly(2026, 1, 1), null, IsCurrentBelt: false),
            coach.Id, isCoach: true);

        await _members.DidNotReceiveWithAnyArgs().ClearCurrentBeltAsync(default!, default);
    }

    [Fact]
    public async Task DeleteBelt_BelongingToAnotherMember_IsNotFound()
    {
        // Without the owner check a coach could delete any belt row by pairing its id with a
        // member they are allowed to reach.
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.GetBeltRecordAsync(9, Arg.Any<CancellationToken>())
            .Returns(new MemberBelt { Id = 9, MemberId = "somebody-else", BeltId = 1 });
        var service = Build(coach, member);

        var result = await service.DeleteBeltAsync(member.Id, 9, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.NotFound, result.ErrorKind);
        await _members.DidNotReceiveWithAnyArgs().RemoveBeltAsync(default!, default);
    }

    [Fact]
    public async Task DeleteBelt_ThatWasCurrent_PromotesTheLatestRemainingOne()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.GetBeltRecordAsync(9, Arg.Any<CancellationToken>())
            .Returns(new MemberBelt { Id = 9, MemberId = member.Id, BeltId = 1, IsCurrentBelt = true });
        var service = Build(coach, member);

        var result = await service.DeleteBeltAsync(member.Id, 9, coach.Id, isCoach: true);

        Assert.True(result.Success);
        await _members.Received(1).PromoteLatestBeltToCurrentAsync(member.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteBelt_ThatWasNotCurrent_PromotesNothing()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        _members.GetBeltRecordAsync(9, Arg.Any<CancellationToken>())
            .Returns(new MemberBelt { Id = 9, MemberId = member.Id, BeltId = 1, IsCurrentBelt = false });
        var service = Build(coach, member);

        await service.DeleteBeltAsync(member.Id, 9, coach.Id, isCoach: true);

        await _members.DidNotReceiveWithAnyArgs().PromoteLatestBeltToCurrentAsync(default!, default);
    }

    [Fact]
    public async Task DeleteBelt_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.DeleteBeltAsync(member.Id, 9, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _members.DidNotReceiveWithAnyArgs().RemoveBeltAsync(default!, default);
    }

    // ---- Editing -----------------------------------------------------------------------------

    [Fact]
    public async Task Update_AsMemberOfAnotherProfile_IsForbidden()
    {
        var member = User();
        var stranger = User();
        var service = Build(member, stranger);

        var result = await service.UpdateMemberAsync(
            stranger.Id,
            new EditMemberDto("A", "B", "a@test.local", null, new DateOnly(2000, 1, 1), null, null),
            member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }
}
