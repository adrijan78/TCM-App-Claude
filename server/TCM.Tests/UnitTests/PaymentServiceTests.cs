using Microsoft.Extensions.Options;
using NSubstitute;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Payments;
using TCM.Application.Options;
using TCM.Application.Services;
using TCM.Domain.Entities;
using static TCM.Tests.UnitTests.TestDoubles;

namespace TCM.Tests.UnitTests;

/// <summary>
/// SPEC section 3.2's rule in isolation: a payment row is written only after the provider says
/// the session was paid, never because a browser reached the success URL. These tests are the
/// ones that would catch that rule being quietly relaxed.
/// </summary>
public class PaymentServiceTests
{
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly ICheckoutService _checkout = Substitute.For<ICheckoutService>();

    private PaymentService Build(params ApplicationUser[] users) => new(
        _payments,
        _checkout,
        UserManagerFor(users),
        Options.Create(new StripeSettings { MembershipDays = 30 }),
        Logger<PaymentService>());

    /// <summary>Nothing was inserted and nothing was committed.</summary>
    private async Task AssertNothingRecorded()
    {
        await _payments.DidNotReceiveWithAnyArgs().AddIfSessionUnusedAsync(default!, default);
        await _payments.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // ---- Confirming an online payment --------------------------------------------------------

    [Fact]
    public async Task Confirm_WithAnUnverifiedSession_RecordsNothing()
    {
        // The whole of SPEC section 3.2 in one test: arriving back at the success URL proves
        // nothing, because anyone can navigate there directly.
        var member = User();
        _payments.GetByStripeSessionIdAsync("sess_fake", Arg.Any<CancellationToken>()).Returns((Payment?)null);
        _checkout.VerifyAsync("sess_fake", Arg.Any<CancellationToken>())
            .Returns(new CheckoutVerification(IsPaid: false, "sess_fake", member.Id));
        var service = Build(member);

        var result = await service.ConfirmAsync("sess_fake", member.Id);

        Assert.False(result.Success);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task Confirm_WithNoSessionId_RecordsNothing()
    {
        var member = User();
        var service = Build(member);

        var result = await service.ConfirmAsync("   ", member.Id);

        Assert.False(result.Success);
        await _checkout.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task Confirm_OfSomeoneElsesVerifiedSession_IsForbidden()
    {
        // The provider says whose payment it was. Crediting the caller instead would let anyone
        // holding a session id extend their own membership on another member's money.
        var caller = User();
        var payer = User();
        _payments.GetByStripeSessionIdAsync("sess_1", Arg.Any<CancellationToken>()).Returns((Payment?)null);
        _checkout.VerifyAsync("sess_1", Arg.Any<CancellationToken>())
            .Returns(new CheckoutVerification(IsPaid: true, "sess_1", payer.Id));
        var service = Build(caller, payer);

        var result = await service.ConfirmAsync("sess_1", caller.Id);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task Confirm_OfAVerifiedSession_RecordsExactlyOnePayment()
    {
        var member = User();
        _payments.GetByStripeSessionIdAsync("sess_ok", Arg.Any<CancellationToken>()).Returns((Payment?)null);
        _checkout.VerifyAsync("sess_ok", Arg.Any<CancellationToken>())
            .Returns(new CheckoutVerification(IsPaid: true, "sess_ok", member.Id));
        _payments.AddIfSessionUnusedAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(call => (true, call.Arg<Payment>()));
        var service = Build(member);

        var result = await service.ConfirmAsync("sess_ok", member.Id);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsPaidOnline);
        await _payments.Received(1).AddIfSessionUnusedAsync(
            Arg.Is<Payment>(p => p.MemberId == member.Id && p.StripeSessionId == "sess_ok"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_Twice_DoesNotWriteASecondRow()
    {
        // A refreshed success page or a retried webhook must find the existing row.
        var member = User();
        var existing = new Payment
        {
            Id = 3,
            MemberId = member.Id,
            IsPaidOnline = true,
            PaymentDate = DateTime.UtcNow,
            NextPaymentDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            StripeSessionId = "sess_repeat"
        };
        _payments.GetByStripeSessionIdAsync("sess_repeat", Arg.Any<CancellationToken>()).Returns(existing);
        var service = Build(member);

        var result = await service.ConfirmAsync("sess_repeat", member.Id);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Id);
        await _checkout.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task Confirm_OfAnExistingSessionBelongingToSomeoneElse_IsForbidden()
    {
        // The early idempotency exit checks ownership too, or a session id would be enough to
        // read whose payment it was and when.
        var caller = User();
        var payer = User();
        _payments.GetByStripeSessionIdAsync("sess_x", Arg.Any<CancellationToken>())
            .Returns(new Payment
            {
                Id = 9,
                MemberId = payer.Id,
                IsPaidOnline = true,
                PaymentDate = DateTime.UtcNow,
                NextPaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                StripeSessionId = "sess_x"
            });
        var service = Build(caller, payer);

        var result = await service.ConfirmAsync("sess_x", caller.Id);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Confirm_WhenARacingCallWon_ReturnsTheWinningRow()
    {
        var member = User();
        var winner = new Payment
        {
            Id = 11,
            MemberId = member.Id,
            IsPaidOnline = true,
            PaymentDate = DateTime.UtcNow,
            NextPaymentDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            StripeSessionId = "sess_race"
        };
        _payments.GetByStripeSessionIdAsync("sess_race", Arg.Any<CancellationToken>()).Returns((Payment?)null);
        _checkout.VerifyAsync("sess_race", Arg.Any<CancellationToken>())
            .Returns(new CheckoutVerification(IsPaid: true, "sess_race", member.Id));
        _payments.AddIfSessionUnusedAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns((false, winner));
        var service = Build(member);

        var result = await service.ConfirmAsync("sess_race", member.Id);

        Assert.True(result.Success);
        Assert.Equal(11, result.Data!.Id);
    }

    // ---- Starting a checkout -----------------------------------------------------------------

    [Fact]
    public async Task StartCheckout_PaysForTheCallerAndNobodyElse()
    {
        var member = User();
        _checkout.CreateSessionAsync(member.Id, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CheckoutSession("sess_new", "http://localhost:4200/pay"));
        var service = Build(member);

        var result = await service.StartCheckoutAsync(member.Id);

        Assert.True(result.Success);
        await _checkout.Received(1).CreateSessionAsync(member.Id, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartCheckout_WhenTheProviderReturnsNothing_ReportsAnExternalFailure()
    {
        var member = User();
        _checkout.CreateSessionAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((CheckoutSession?)null);
        var service = Build(member);

        var result = await service.StartCheckoutAsync(member.Id);

        Assert.False(result.Success);
        Assert.Equal(ErrorKind.External, result.ErrorKind);
    }

    // ---- The club-wide table -----------------------------------------------------------------

    [Fact]
    public async Task GetClubPayments_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.GetClubPaymentsAsync(member.Id, isCoach: false, null, null, null, null);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _payments.DidNotReceiveWithAnyArgs().GetClubHistoryAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task GetClubPayments_ScopesToTheCoachsOwnClub()
    {
        var coach = User(clubId: 5, isCoach: true);
        _payments.GetClubHistoryAsync(5, null, null, null, null, Arg.Any<CancellationToken>()).Returns([]);
        var service = Build(coach);

        var result = await service.GetClubPaymentsAsync(coach.Id, isCoach: true, null, null, null, null);

        Assert.True(result.Success);
        await _payments.Received(1).GetClubHistoryAsync(5, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(13, null)]
    [InlineData(0, null)]
    [InlineData(null, 1999)]
    [InlineData(null, 2101)]
    public async Task GetClubPayments_WithAnOutOfRangeFilter_IsRejected(int? month, int? year)
    {
        var coach = User(isCoach: true);
        var service = Build(coach);

        var result = await service.GetClubPaymentsAsync(coach.Id, isCoach: true, year, month, null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetClubPayments_TranslatesTheMethodFilterToTheStoredBoolean()
    {
        var coach = User(clubId: 5, isCoach: true);
        _payments.GetClubHistoryAsync(5, null, null, null, true, Arg.Any<CancellationToken>()).Returns([]);
        var service = Build(coach);

        await service.GetClubPaymentsAsync(coach.Id, isCoach: true, null, null, null, PaymentMethod.Online);

        await _payments.Received(1).GetClubHistoryAsync(5, null, null, null, true, Arg.Any<CancellationToken>());
    }

    // ---- One member's history ----------------------------------------------------------------

    [Fact]
    public async Task GetMemberHistory_AsMemberReachingForAnotherId_IsForbidden()
    {
        var member = User();
        var stranger = User();
        var service = Build(member, stranger);

        var result = await service.GetMemberHistoryAsync(stranger.Id, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await _payments.DidNotReceiveWithAnyArgs().GetMemberHistoryAsync(default!, default);
    }

    [Fact]
    public async Task GetMemberHistory_AsCoachFromAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        var outsider = User(clubId: 2);
        var service = Build(coach, outsider);

        var result = await service.GetMemberHistoryAsync(outsider.Id, coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
    }

    // ---- Cash payments -----------------------------------------------------------------------

    [Fact]
    public async Task RecordCash_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.RecordCashPaymentAsync(
            new CashPaymentDto(member.Id, null), member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task RecordCash_ForAMemberOfAnotherClub_IsForbidden()
    {
        var coach = User(clubId: 1, isCoach: true);
        var outsider = User(clubId: 2);
        var service = Build(coach, outsider);

        var result = await service.RecordCashPaymentAsync(
            new CashPaymentDto(outsider.Id, null), coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task RecordCash_DatedInTheFuture_IsRejected()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        var service = Build(coach, member);

        var result = await service.RecordCashPaymentAsync(
            new CashPaymentDto(member.Id, DateTime.UtcNow.AddDays(2)), coach.Id, isCoach: true);

        Assert.False(result.Success);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task RecordCash_DatedBeforeTheMemberJoined_IsRejected()
    {
        var coach = User(clubId: 1, isCoach: true);
        var member = User(clubId: 1);
        member.StartedOn = new DateOnly(2025, 6, 1);
        var service = Build(coach, member);

        var result = await service.RecordCashPaymentAsync(
            new CashPaymentDto(member.Id, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)),
            coach.Id, isCoach: true);

        Assert.False(result.Success);
        await AssertNothingRecorded();
    }

    [Fact]
    public async Task RecordCash_ForAnUnknownMember_IsNotFound()
    {
        var coach = User(clubId: 1, isCoach: true);
        var service = Build(coach);

        var result = await service.RecordCashPaymentAsync(
            new CashPaymentDto("no-such-id", null), coach.Id, isCoach: true);

        Assert.Equal(ErrorKind.NotFound, result.ErrorKind);
    }

    // ---- Deleting ----------------------------------------------------------------------------

    [Fact]
    public async Task Delete_AsMember_IsForbidden()
    {
        var member = User();
        var service = Build(member);

        var result = await service.DeleteAsync(1, member.Id, isCoach: false);

        Assert.Equal(ErrorKind.Forbidden, result.ErrorKind);
        _payments.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task Delete_OfAPaymentOutsideTheCoachsClub_RemovesNothing()
    {
        // FindInClubAsync returning null means "not there, or not yours" and the service must
        // not tell the two apart.
        var coach = User(clubId: 1, isCoach: true);
        _payments.FindInClubAsync(7, 1, Arg.Any<CancellationToken>()).Returns((Payment?)null);
        var service = Build(coach);

        var result = await service.DeleteAsync(7, coach.Id, isCoach: true);

        Assert.False(result.Success);
        _payments.DidNotReceiveWithAnyArgs().Remove(default!);
    }
}
