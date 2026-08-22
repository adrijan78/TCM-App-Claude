using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Dtos.Payments;

namespace TCM.Tests.Integration;

/// <summary>
/// The payment-record endpoints of SPEC sections 6.4 (a member's Membership tab) and 6.7 (the
/// coach's club-wide payments page), plus the next-due-date rule that cash and online share.
/// </summary>
/// <remarks>
/// The fixture's database is shared by every test in this class, so nothing here asserts an
/// absolute due date or a row count. Each assertion is relative to what the endpoint itself
/// reported a moment earlier, which keeps the tests independent of execution order.
/// </remarks>
public class PaymentsEndpointTests(TcmApiFactory factory) : IClassFixture<TcmApiFactory>
{
    /// <summary>Matches Stripe:MembershipDays in <see cref="TcmApiFactory"/>.</summary>
    private const int MembershipDays = 30;

    // ---- Club-wide payments, SPEC 6.7 -----------------------------------------------------------

    [Fact]
    public async Task GetClubPayments_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/payments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetClubPayments_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync("/api/payments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetClubPayments_AsCoach_Returns200_AndIncludesTheClubsPayments()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var logged = await LogCashAsync(coach, factory.MemberId);

        var response = await coach.GetAsync("/api/payments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await ReadAsync<IReadOnlyList<PaymentsDto>>(response);
        Assert.Contains(rows, p => p.Id == logged.Id);
        // The list is a club-wide view, so it carries the member's name for the table column.
        Assert.All(rows, p => Assert.False(string.IsNullOrWhiteSpace(p.MemberFullName)));
    }

    [Fact]
    public async Task GetClubPayments_FilteredByMethod_ReturnsOnlyThatMethod()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        await LogCashAsync(coach, factory.OtherMemberId);
        await PayOnlineAsync(member);

        var cash = await ReadAsync<IReadOnlyList<PaymentsDto>>(await coach.GetAsync("/api/payments?method=Cash"));
        var online = await ReadAsync<IReadOnlyList<PaymentsDto>>(await coach.GetAsync("/api/payments?method=Online"));

        Assert.NotEmpty(cash);
        Assert.NotEmpty(online);
        Assert.All(cash, p => Assert.False(p.IsPaidOnline));
        Assert.All(online, p => Assert.True(p.IsPaidOnline));
    }

    [Fact]
    public async Task GetClubPayments_FilteredByMember_ReturnsOnlyThatMember()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await LogCashAsync(coach, factory.OtherMemberId);

        var response = await coach.GetAsync($"/api/payments?memberId={factory.OtherMemberId}");

        var rows = await ReadAsync<IReadOnlyList<PaymentsDto>>(response);
        Assert.NotEmpty(rows);
        Assert.All(rows, p => Assert.Equal(factory.OtherMemberId, p.MemberId));
    }

    [Fact]
    public async Task GetClubPayments_FilteredByAYearWithNoPayments_ReturnsNothing()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await LogCashAsync(coach, factory.MemberId);

        var response = await coach.GetAsync("/api/payments?year=2001&month=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ReadAsync<IReadOnlyList<PaymentsDto>>(response));
    }

    [Fact]
    public async Task GetClubPayments_WithAnImpossibleMonth_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/payments?month=13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<PaymentsDto>>>();
        Assert.False(body!.Success);
    }

    [Fact]
    public async Task GetClubPayments_NeverLeaksStripeIdentifiers()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        await PayOnlineAsync(member);

        var raw = await (await coach.GetAsync("/api/payments")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("session", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stripe", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ---- A member's payment history, SPEC 6.4 ---------------------------------------------------

    [Fact]
    public async Task GetMemberHistory_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/payments/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMemberHistory_OfAnotherMember_AsMember_Returns403()
    {
        // The IDOR this whole authorization model exists to stop: change the id in the URL.
        var stranger = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);

        var response = await stranger.GetAsync($"/api/payments/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberPaymentHistoryDto>>();
        Assert.False(body!.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task GetMemberHistory_OfSelf_AsMember_Returns200()
    {
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await member.GetAsync($"/api/payments/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await ReadAsync<MemberPaymentHistoryDto>(response);
        Assert.Equal(factory.MemberId, history.MemberId);
        Assert.All(history.Payments, p => Assert.Equal(factory.MemberId, p.MemberId));
    }

    [Fact]
    public async Task GetMemberHistory_OfAMemberInOwnClub_AsCoach_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync($"/api/payments/member/{factory.OtherMemberId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(factory.OtherMemberId, (await ReadAsync<MemberPaymentHistoryDto>(response)).MemberId);
    }

    [Fact]
    public async Task GetMemberHistory_OfAnUnknownMember_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/payments/member/no-such-member-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMemberHistory_CarriesTheNextDueDateBanner()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var logged = await LogCashAsync(coach, factory.MemberId);

        var history = await ReadAsync<MemberPaymentHistoryDto>(
            await coach.GetAsync($"/api/payments/member/{factory.MemberId}"));

        // The banner shows the furthest due date the member holds, which the payment just made
        // is: it extended from whatever came before it.
        Assert.Equal(logged.NextPaymentDate, history.Membership.NextPaymentDate);
        Assert.False(history.Membership.IsOverdue);
        Assert.True(history.Membership.DaysUntilDue > 0);
    }

    // ---- Cash payments, SPEC 5 (coach only) -----------------------------------------------------

    [Fact]
    public async Task LogCash_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments/cash", new CashPaymentDto(factory.MemberId, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogCash_AsMember_Returns403()
    {
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await member.PostAsJsonAsync("/api/payments/cash", new CashPaymentDto(factory.MemberId, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LogCash_AsCoach_RecordsAnOfflinePayment()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var payment = await LogCashAsync(coach, factory.MemberId);

        Assert.False(payment.IsPaidOnline);
        Assert.Equal(factory.MemberId, payment.MemberId);

        var history = await ReadAsync<MemberPaymentHistoryDto>(
            await coach.GetAsync($"/api/payments/member/{factory.MemberId}"));
        Assert.Contains(history.Payments, p => p.Id == payment.Id);
    }

    [Fact]
    public async Task LogCash_DatedInTheFuture_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync("/api/payments/cash",
            new CashPaymentDto(factory.MemberId, DateTime.UtcNow.AddDays(2)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentsDto>>();
        Assert.False(body!.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task LogCash_ForAnUnknownMember_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync("/api/payments/cash",
            new CashPaymentDto("no-such-member-id", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- The shared next-due-date rule -----------------------------------------------------------

    [Fact]
    public async Task CashAndOnline_UseTheSameNextDueDateRule()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var beforeCash = await NextDueAsync(coach, factory.OtherMemberId);
        var cash = await LogCashAsync(coach, factory.OtherMemberId);

        var beforeOnline = await NextDueAsync(member, factory.MemberId);
        var online = await PayOnlineAsync(member);

        // One rule: extend from the current due date if it is still in the future, otherwise
        // from today. Both routes into Payments must land on it.
        Assert.Equal(ExpectedDue(beforeCash), cash.NextPaymentDate);
        Assert.Equal(ExpectedDue(beforeOnline), online.NextPaymentDate);
        Assert.False(cash.IsPaidOnline);
        Assert.True(online.IsPaidOnline);
    }

    [Fact]
    public async Task RenewingEarly_ExtendsFromTheCurrentDueDate_NotFromToday()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var first = await LogCashAsync(coach, factory.OtherMemberId);
        var second = await LogCashAsync(coach, factory.OtherMemberId);

        // Paying again while still covered must add a period to the end, not reset the clock.
        Assert.Equal(first.NextPaymentDate.AddDays(MembershipDays), second.NextPaymentDate);
        Assert.True(second.NextPaymentDate > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(MembershipDays));
    }

    // ---- Deleting a record, SPEC 6.4 and 6.7 -----------------------------------------------------

    [Fact]
    public async Task Delete_Anonymously_Returns401()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var payment = await LogCashAsync(coach, factory.MemberId);

        var response = await factory.CreateClient().DeleteAsync($"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsMember_Returns403_EvenForTheirOwnPayment()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var payment = await LogCashAsync(coach, factory.MemberId);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.DeleteAsync($"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsCoach_RemovesTheRecord()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var payment = await LogCashAsync(coach, factory.MemberId);

        var response = await coach.DeleteAsync($"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await ReadAsync<MemberPaymentHistoryDto>(
            await coach.GetAsync($"/api/payments/member/{factory.MemberId}"));
        Assert.DoesNotContain(history.Payments, p => p.Id == payment.Id);
    }

    [Fact]
    public async Task Delete_OfAnUnknownPayment_AsCoach_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.DeleteAsync("/api/payments/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private async Task<HttpClient> ClientAsAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/account/login",
            new LoginMemberDto(email, TcmApiFactory.Password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
        Assert.True(body!.Success, body.Message);
        return body.Data!;
    }

    private async Task<PaymentsDto> LogCashAsync(HttpClient coach, string memberId)
    {
        var response = await coach.PostAsJsonAsync("/api/payments/cash", new CashPaymentDto(memberId, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<PaymentsDto>(response);
    }

    /// <summary>Runs the real online path — checkout session, then server-side verification.</summary>
    private static async Task<PaymentsDto> PayOnlineAsync(HttpClient member)
    {
        var started = await member.PostAsync("/api/stripe/checkout-session", null);
        started.EnsureSuccessStatusCode();

        var session = await ReadAsync<CheckoutSessionDto>(started);
        var sessionId = System.Web.HttpUtility.ParseQueryString(new Uri(session.RedirectUrl).Query)["session_id"]!;

        var confirmed = await member.PostAsJsonAsync("/api/stripe/confirm", new ConfirmPaymentDto(sessionId));
        return await ReadAsync<PaymentsDto>(confirmed);
    }

    private static async Task<DateOnly?> NextDueAsync(HttpClient client, string memberId)
    {
        var history = await ReadAsync<MemberPaymentHistoryDto>(
            await client.GetAsync($"/api/payments/member/{memberId}"));
        return history.Membership.NextPaymentDate;
    }

    /// <summary>The rule, restated independently of the implementation under test.</summary>
    private static DateOnly ExpectedDue(DateOnly? currentDue)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startFrom = currentDue is not null && currentDue > today ? currentDue.Value : today;
        return startFrom.AddDays(MembershipDays);
    }
}
