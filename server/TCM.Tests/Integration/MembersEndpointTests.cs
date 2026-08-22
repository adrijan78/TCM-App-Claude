using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Dtos.Members;
using TCM.Domain.Entities;
using TCM.Infrastructure.Persistence;

namespace TCM.Tests.Integration;

/// <summary>
/// The Members slice of SPEC sections 6.3 and 6.4, read as an authorization matrix. Every route
/// is exercised four ways — anonymous, a member reaching for someone else's data, a member
/// reaching for their own, and the coach — because the role attribute alone cannot tell the
/// middle two apart.
/// </summary>
/// <remarks>
/// Tests that change a member create their own through the coach's registration route. Sharing
/// the fixture's three accounts for mutations would make the suite order-dependent, and
/// deactivating one of them would stop every later test logging in as it.
/// </remarks>
public class MembersEndpointTests(TcmApiFactory factory) : IClassFixture<TcmApiFactory>
{
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

    /// <summary>
    /// Registers a member through the real coach-only route, so the new account lands in the
    /// coach's club exactly as it would in the app.
    /// </summary>
    private async Task<(string Id, string Email)> RegisterMemberAsync(
        string firstName = "Fresh", string? lastName = null, DateOnly? dateOfBirth = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"m-{suffix}@test.local";

        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var response = await coach.PostAsJsonAsync("/api/account/register", new MemberRegisterDto(
            FirstName: firstName,
            LastName: lastName ?? $"Recruit{suffix}",
            Email: email,
            Password: TcmApiFactory.Password,
            Height: 170m,
            Weight: 62m,
            DateOfBirth: dateOfBirth ?? new DateOnly(2000, 6, 1),
            BeltId: 1,
            Role: "Member"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisteredMemberDto>>();
        return (body!.Data!.Id, email);
    }

    /// <summary>
    /// The fixture seeds roles, a club and three accounts but no belts, so the belt lookup rows
    /// these tests need are created directly. Additive and idempotent.
    /// </summary>
    private async Task<int> EnsureBeltAsync(string beltName, int rank)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await db.Belts.FirstOrDefaultAsync(b => b.BeltName == beltName);
        if (existing is not null) return existing.Id;

        var belt = new Belt { BeltName = beltName, Rank = rank };
        db.Belts.Add(belt);
        await db.SaveChangesAsync();
        return belt.Id;
    }

    /// <summary>
    /// Removes the belt a member is given at registration (SPEC 6.1: the registration form asks
    /// for one). The belt-invariant tests below want a member holding no belts at all, so they
    /// can exercise "the first belt recorded is forced current" through the public API.
    /// </summary>
    private async Task ClearBeltsAsync(string memberId)
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var history = await coach.GetAsync($"/api/members/{memberId}/belts");
        var body = await history.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberBeltDto>>>();

        foreach (var record in body!.Data!)
        {
            var deleted = await coach.DeleteAsync($"/api/members/{memberId}/belts/{record.Id}");
            deleted.EnsureSuccessStatusCode();
        }
    }

    private static EditMemberDto EditOf(MemberDto member) => new(
        member.FirstName,
        member.LastName,
        member.Email,
        member.PhoneNumber,
        member.DateOfBirth,
        member.Height,
        member.Weight);

    private static async Task<MemberDto> ReadMemberAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberDto>>();
        Assert.True(body!.Success, body.Message);
        return body.Data!;
    }

    private static DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    // ---- GET /api/members — coach only (SPEC section 6.3) --------------------------------------

    [Fact]
    public async Task List_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync("/api/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_AsCoach_ReturnsMembersOfOwnClubWithDerivedAgeAndStatus()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberDto>>>();
        Assert.True(body!.Success);

        var member = Assert.Single(body.Data!, m => m.Id == factory.MemberId);
        Assert.Equal("Test", member.FirstName);
        Assert.True(member.IsActive);
        Assert.False(member.IsCoach);
        Assert.Equal(new DateOnly(2024, 1, 1), member.StartedOn);
        Assert.Equal(AgeOn(new DateOnly(2000, 1, 1)), member.Age);
    }

    [Fact]
    public async Task List_IncludesDeactivatedMembers()
    {
        // SPEC section 6.3: the table shows all members "regardless of status".
        var (id, _) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        await coach.PatchAsync($"/api/members/{id}/deactivate", null);
        var response = await coach.GetAsync("/api/members");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberDto>>>();
        var listed = Assert.Single(body!.Data!, m => m.Id == id);
        Assert.False(listed.IsActive);
    }

    [Fact]
    public async Task List_FilteredByName_NarrowsTheResults()
    {
        var (id, _) = await RegisterMemberAsync(firstName: "Zephyr", lastName: "Quicksilver");
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/members?search=Quicksilver");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberDto>>>();
        Assert.True(body!.Success);
        Assert.Equal(id, Assert.Single(body.Data!).Id);
    }

    [Fact]
    public async Task List_FilteredByEmail_NarrowsTheResults()
    {
        var (id, email) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync($"/api/members?search={email}");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberDto>>>();
        Assert.Equal(id, Assert.Single(body!.Data!).Id);
    }

    [Fact]
    public async Task List_FilteredByAgeGroup_UsesTheBandsNotTheStoredDate()
    {
        var (kidId, _) = await RegisterMemberAsync(dateOfBirth: TodayUtc.AddYears(-8));
        var (seniorId, _) = await RegisterMemberAsync(dateOfBirth: TodayUtc.AddYears(-30));
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync("/api/members?ageGroup=Kids");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberDto>>>();
        Assert.True(body!.Success);
        Assert.Contains(body.Data!, m => m.Id == kidId);
        Assert.DoesNotContain(body.Data!, m => m.Id == seniorId);
        Assert.All(body.Data!, m => Assert.InRange(m.Age, 0, 11));
    }

    [Fact]
    public async Task List_FilteredByBelt_ReturnsOnlyMembersHoldingItNow()
    {
        var beltId = await EnsureBeltAsync("Filter Blue", 71);
        var (holderId, _) = await RegisterMemberAsync();
        var (otherId, _) = await RegisterMemberAsync();

        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await coach.PostAsJsonAsync($"/api/members/{holderId}/belts",
            new AddMemberBeltDto(beltId, TodayUtc.AddDays(-1), null, true));

        var response = await coach.GetAsync($"/api/members?beltId={beltId}");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberDto>>>();
        Assert.Contains(body!.Data!, m => m.Id == holderId);
        Assert.DoesNotContain(body.Data!, m => m.Id == otherId);
    }

    // ---- GET /api/members/{id} — the ownership check that matters most -------------------------

    [Fact]
    public async Task Get_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/members/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_OwnProfile_AsMember_Returns200()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync($"/api/members/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await ReadMemberAsync(response);
        Assert.Equal(factory.MemberId, member.Id);
    }

    [Fact]
    public async Task Get_AnotherMembersProfile_AsMember_Returns403()
    {
        // Changing an id in the URL must never reach another member's record.
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync($"/api/members/{factory.OtherMemberId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberDto>>();
        Assert.False(body!.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task Get_TheCoachsProfile_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync($"/api/members/{factory.CoachId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AnyMemberInOwnClub_AsCoach_Returns200()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.GetAsync($"/api/members/{factory.OtherMemberId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await ReadMemberAsync(response);
        Assert.Equal(factory.OtherMemberId, member.Id);
    }

    [Fact]
    public async Task Get_UnknownMember_AsCoach_Returns404()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.GetAsync($"/api/members/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_NeverLeaksCredentialOrBillingFields()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.GetAsync($"/api/members/{factory.MemberId}");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stripeCustomerId", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ---- PUT /api/members/{id} ------------------------------------------------------------------

    [Fact]
    public async Task Update_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/members/{factory.MemberId}",
            new EditMemberDto("Hack", "Attempt", "hack@test.local", null, new DateOnly(2000, 1, 1), null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_AnotherMember_AsMember_Returns403()
    {
        var (victimId, _) = await RegisterMemberAsync();
        var attacker = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await attacker.PutAsJsonAsync($"/api/members/{victimId}",
            new EditMemberDto("Owned", "Now", "owned@test.local", null, new DateOnly(2000, 1, 1), null, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnProfile_AsMember_Succeeds()
    {
        var (id, email) = await RegisterMemberAsync();
        var client = await ClientAsAsync(email);

        var current = await ReadMemberAsync(await client.GetAsync($"/api/members/{id}"));
        var response = await client.PutAsJsonAsync($"/api/members/{id}",
            EditOf(current) with { FirstName = "Renamed", Height = 181m, PhoneNumber = "070123456" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadMemberAsync(response);
        Assert.Equal("Renamed", updated.FirstName);
        Assert.Equal(181m, updated.Height);
        Assert.Equal("070123456", updated.PhoneNumber);
    }

    [Fact]
    public async Task Update_AsMember_CannotPromoteOrReactivateThroughTheBody()
    {
        // The privileged fields are not part of EditMemberDto at all, so a request body that
        // carries them binds without them. This asserts the outcome rather than the mechanism.
        var (id, email) = await RegisterMemberAsync();
        var client = await ClientAsAsync(email);

        var payload = $$"""
            {
              "firstName": "Sneaky",
              "lastName": "Escalation",
              "email": "{{email}}",
              "phoneNumber": null,
              "dateOfBirth": "2000-06-01",
              "height": 170,
              "weight": 62,
              "isCoach": true,
              "isActive": false,
              "clubId": 999,
              "role": "Coach"
            }
            """;

        var response = await client.PutAsync($"/api/members/{id}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadMemberAsync(response);
        Assert.Equal("Sneaky", updated.FirstName);
        Assert.False(updated.IsCoach);
        Assert.True(updated.IsActive);

        // And the coach-only list still refuses them, which is the thing IsCoach would have bought.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/members")).StatusCode);
    }

    [Fact]
    public async Task Update_AnyMemberInOwnClub_AsCoach_Succeeds()
    {
        var (id, _) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var current = await ReadMemberAsync(await coach.GetAsync($"/api/members/{id}"));
        var response = await coach.PutAsJsonAsync($"/api/members/{id}",
            EditOf(current) with { LastName = "CoachEdited" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CoachEdited", (await ReadMemberAsync(response)).LastName);
    }

    [Fact]
    public async Task Update_WithInvalidDetails_Returns400WithErrors()
    {
        var (id, email) = await RegisterMemberAsync();
        var client = await ClientAsAsync(email);

        var response = await client.PutAsJsonAsync($"/api/members/{id}",
            new EditMemberDto("", "Nobody", "not-an-email", null, new DateOnly(2400, 1, 1), 900m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberDto>>();
        Assert.False(body!.Success);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task Update_ToAnExistingEmail_Returns409()
    {
        var (id, email) = await RegisterMemberAsync();
        var client = await ClientAsAsync(email);

        var current = await ReadMemberAsync(await client.GetAsync($"/api/members/{id}"));
        var response = await client.PutAsJsonAsync($"/api/members/{id}",
            EditOf(current) with { Email = TcmApiFactory.MemberEmail });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- PATCH /api/members/{id}/deactivate — coach only ----------------------------------------

    [Fact]
    public async Task Deactivate_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PatchAsync($"/api/members/{factory.MemberId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_AsMember_Returns403()
    {
        var (victimId, _) = await RegisterMemberAsync();
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PatchAsync($"/api/members/{victimId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_OwnAccount_AsMember_IsStillForbidden()
    {
        // The route is coach-only, so "it is my own record" buys a member nothing here.
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PatchAsync($"/api/members/{factory.MemberId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_AsCoach_ClearsTheFlagButKeepsTheRow()
    {
        var (id, email) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PatchAsync($"/api/members/{id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await ReadMemberAsync(response)).IsActive);

        // Never deleted: history depends on the row, so it must still be readable afterwards.
        var afterwards = await coach.GetAsync($"/api/members/{id}");
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
        Assert.False((await ReadMemberAsync(afterwards)).IsActive);

        // But the account can no longer sign in (SPEC section 6.3).
        var login = await factory.CreateClient().PostAsJsonAsync("/api/account/login",
            new LoginMemberDto(email, TcmApiFactory.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Twice_IsHarmless()
    {
        var (id, _) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        await coach.PatchAsync($"/api/members/{id}/deactivate", null);
        var second = await coach.PatchAsync($"/api/members/{id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.False((await ReadMemberAsync(second)).IsActive);
    }

    [Fact]
    public async Task Deactivate_Self_AsCoach_IsRefused()
    {
        // 1 coach : 1 club — a coach who locked themselves out has nobody to let them back in.
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PatchAsync($"/api/members/{factory.CoachId}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stillActive = await coach.GetAsync($"/api/members/{factory.CoachId}");
        Assert.True((await ReadMemberAsync(stillActive)).IsActive);
    }

    [Fact]
    public async Task Deactivate_UnknownMember_AsCoach_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PatchAsync($"/api/members/{Guid.NewGuid()}/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- GET /api/members/{id}/belts -------------------------------------------------------------

    [Fact]
    public async Task GetBelts_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/members/{factory.MemberId}/belts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBelts_OfAnotherMember_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync($"/api/members/{factory.OtherMemberId}/belts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBelts_OwnHistory_AsMember_Returns200()
    {
        var beltId = await EnsureBeltAsync("History Green", 72);
        var (id, email) = await RegisterMemberAsync();

        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(beltId, TodayUtc.AddDays(-3), "Graded well.", true));

        var member = await ClientAsAsync(email);
        var response = await member.GetAsync($"/api/members/{id}/belts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberBeltDto>>>();
        // Two records: the belt given at registration, and the one the coach just awarded.
        Assert.Equal(2, body!.Data!.Count);
        var record = Assert.Single(body.Data!, b => b.Belt.BeltName == "History Green");
        Assert.True(record.IsCurrentBelt);
        // Still exactly one current belt across the whole history (SPEC section 4).
        Assert.Single(body.Data!, b => b.IsCurrentBelt);
    }

    // ---- POST /api/members/{id}/belts — coach only -----------------------------------------------

    [Fact]
    public async Task AddBelt_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/members/{factory.MemberId}/belts",
            new AddMemberBeltDto(1, TodayUtc, null, true));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddBelt_ToOwnProfile_AsMember_Returns403()
    {
        // Belt exams are the coach's to record — a member may view them only (SPEC section 5).
        var beltId = await EnsureBeltAsync("Self Promotion", 73);
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PostAsJsonAsync($"/api/members/{factory.MemberId}/belts",
            new AddMemberBeltDto(beltId, TodayUtc, "I promoted myself.", true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddBelt_AsCoach_MakesTheFirstBeltCurrentEvenWhenNotAsked()
    {
        var beltId = await EnsureBeltAsync("First White", 74);
        var (id, _) = await RegisterMemberAsync();
        await ClearBeltsAsync(id);
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(beltId, TodayUtc.AddDays(-10), null, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberBeltDto>>();
        Assert.True(body!.Data!.IsCurrentBelt);
    }

    [Fact]
    public async Task AddBelt_MarkedCurrent_ClearsThePreviousCurrentBelt()
    {
        // The invariant of SPEC section 4: a member accumulates belts, exactly one is current.
        var oldBeltId = await EnsureBeltAsync("Invariant Yellow", 75);
        var newBeltId = await EnsureBeltAsync("Invariant Blue", 76);
        var (id, _) = await RegisterMemberAsync();
        await ClearBeltsAsync(id);
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(oldBeltId, TodayUtc.AddDays(-30), null, true));
        await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(newBeltId, TodayUtc.AddDays(-1), null, true));

        var history = await coach.GetAsync($"/api/members/{id}/belts");
        var body = await history.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberBeltDto>>>();

        Assert.Equal(2, body!.Data!.Count);
        var current = Assert.Single(body.Data!, b => b.IsCurrentBelt);
        Assert.Equal(newBeltId, current.Belt.Id);

        // And the member list agrees about which belt they hold.
        var listed = await coach.GetAsync($"/api/members?beltId={newBeltId}");
        var listBody = await listed.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberDto>>>();
        Assert.Contains(listBody!.Data!, m => m.Id == id && m.CurrentBelt!.Id == newBeltId);
    }

    [Fact]
    public async Task AddBelt_NotMarkedCurrent_LeavesTheExistingCurrentBeltAlone()
    {
        var currentBeltId = await EnsureBeltAsync("Keep Red", 77);
        var backfillBeltId = await EnsureBeltAsync("Backfill White", 78);
        var (id, _) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(currentBeltId, TodayUtc.AddDays(-5), null, true));
        await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(backfillBeltId, TodayUtc.AddDays(-400), "Recorded late.", false));

        var history = await coach.GetAsync($"/api/members/{id}/belts");
        var body = await history.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberBeltDto>>>();

        var current = Assert.Single(body!.Data!, b => b.IsCurrentBelt);
        Assert.Equal(currentBeltId, current.Belt.Id);
    }

    [Fact]
    public async Task AddBelt_WithUnknownBelt_Returns400()
    {
        var (id, _) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(999_999, TodayUtc, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddBelt_DatedInTheFuture_Returns400()
    {
        var beltId = await EnsureBeltAsync("Future Black", 79);
        var (id, _) = await RegisterMemberAsync();
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(beltId, TodayUtc.AddYears(1), null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberBeltDto>>();
        Assert.NotEmpty(body!.Errors);
    }

    // ---- DELETE /api/members/{id}/belts/{beltRecordId} — coach only ------------------------------

    [Fact]
    public async Task DeleteBelt_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/members/{factory.MemberId}/belts/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBelt_AsMember_Returns403()
    {
        var beltId = await EnsureBeltAsync("Undeletable Green", 80);
        var (id, email) = await RegisterMemberAsync();

        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var added = await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(beltId, TodayUtc.AddDays(-2), null, true));
        var record = await added.Content.ReadFromJsonAsync<ApiResponse<MemberBeltDto>>();

        var member = await ClientAsAsync(email);
        var response = await member.DeleteAsync($"/api/members/{id}/belts/{record!.Data!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBelt_AsCoach_RemovesItAndRestoresACurrentBelt()
    {
        var firstBeltId = await EnsureBeltAsync("Promote Yellow", 81);
        var secondBeltId = await EnsureBeltAsync("Promote Blue", 82);
        var (id, _) = await RegisterMemberAsync();
        await ClearBeltsAsync(id);
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(firstBeltId, TodayUtc.AddDays(-60), null, true));
        var latest = await coach.PostAsJsonAsync($"/api/members/{id}/belts",
            new AddMemberBeltDto(secondBeltId, TodayUtc.AddDays(-2), null, true));
        var current = await latest.Content.ReadFromJsonAsync<ApiResponse<MemberBeltDto>>();

        var response = await coach.DeleteAsync($"/api/members/{id}/belts/{current!.Data!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await coach.GetAsync($"/api/members/{id}/belts");
        var body = await history.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberBeltDto>>>();

        var remaining = Assert.Single(body!.Data!);
        Assert.Equal(firstBeltId, remaining.Belt.Id);
        // Deleting the current belt must not leave the member holding none.
        Assert.True(remaining.IsCurrentBelt);
    }

    [Fact]
    public async Task DeleteBelt_ThroughAnotherMembersRoute_Returns404()
    {
        // The belt exam has to belong to the member in the URL, or a coach could delete any row
        // in the table by pairing its id with a member they are allowed to reach.
        var beltId = await EnsureBeltAsync("Mismatch Red", 83);
        var (ownerId, _) = await RegisterMemberAsync();
        var (strangerId, _) = await RegisterMemberAsync();
        await ClearBeltsAsync(ownerId);
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var added = await coach.PostAsJsonAsync($"/api/members/{ownerId}/belts",
            new AddMemberBeltDto(beltId, TodayUtc.AddDays(-4), null, true));
        var record = await added.Content.ReadFromJsonAsync<ApiResponse<MemberBeltDto>>();

        var response = await coach.DeleteAsync($"/api/members/{strangerId}/belts/{record!.Data!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And the record is untouched.
        var history = await coach.GetAsync($"/api/members/{ownerId}/belts");
        var body = await history.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MemberBeltDto>>>();
        Assert.Single(body!.Data!);
    }

    [Fact]
    public async Task DeleteBelt_ThatDoesNotExist_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.DeleteAsync($"/api/members/{factory.MemberId}/belts/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static int AgeOn(DateOnly dateOfBirth)
    {
        var today = TodayUtc;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age;
    }
}
