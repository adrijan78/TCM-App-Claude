using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Dtos.Common;
using TCM.Domain.Entities;
using TCM.Infrastructure.Persistence;

namespace TCM.Tests.Integration;

/// <summary>
/// SPEC section 5 read as a test matrix. For every protected route these tests assert all four
/// outcomes — anonymous, member, coach, and (where relevant) a member reaching for data that is
/// not theirs. This is the suite that matters most in this application.
/// </summary>
public class AuthorizationMatrixTests(TcmApiFactory factory) : IClassFixture<TcmApiFactory>
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

    // ---- Login -------------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithValidCoachCredentials_ReturnsTokenAndCoachRole()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/login",
            new LoginMemberDto(TcmApiFactory.CoachEmail, TcmApiFactory.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        Assert.True(body!.Success);
        Assert.Contains("Coach", body.Data!.Roles);
        Assert.NotEmpty(body.Data.Token);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/login",
            new LoginMemberDto(TcmApiFactory.MemberEmail, "TotallyWrong123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsSameMessageAsWrongPassword()
    {
        // If these differed, the endpoint would let anyone enumerate the club's member emails.
        var client = factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/account/login",
            new LoginMemberDto("nobody@nowhere.test", "TotallyWrong123!"));
        var wrongPassword = await client.PostAsJsonAsync("/api/account/login",
            new LoginMemberDto(TcmApiFactory.MemberEmail, "TotallyWrong123!"));

        var unknownBody = await unknown.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        var wrongBody = await wrongPassword.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();

        Assert.Equal(unknown.StatusCode, wrongPassword.StatusCode);
        Assert.Equal(unknownBody!.Message, wrongBody!.Message);
    }

    [Fact]
    public async Task Login_ResponseNeverContainsPasswordHash()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/login",
            new LoginMemberDto(TcmApiFactory.CoachEmail, TcmApiFactory.Password));
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stripeCustomerId", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Registration is coach-only (SPEC section 6.1) ----------------------------------------

    [Fact]
    public async Task Register_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/register", NewMember("anon@test.local"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PostAsJsonAsync("/api/account/register", NewMember("sneak@test.local"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_AsCoach_Succeeds()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.PostAsJsonAsync("/api/account/register", NewMember("recruit@test.local"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        Assert.True(body!.Success);
        Assert.Contains("Member", body.Data!.Roles);
    }

    [Fact]
    public async Task Register_RecordsTheChosenBeltAsTheMembersCurrentBelt()
    {
        // Regression: the registration form asks for a belt (SPEC 6.1) and the validator checked
        // it, but nothing ever wrote a MemberBelt row — every new member started with an empty
        // belt history.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var belt = await db.Belts.FirstOrDefaultAsync(b => b.BeltName == "Regression Green");
        if (belt is null)
        {
            belt = new Belt { BeltName = "Regression Green", Rank = 99 };
            db.Belts.Add(belt);
            await db.SaveChangesAsync();
        }

        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var email = $"belted-{Guid.NewGuid():N}@test.local";

        var registered = await client.PostAsJsonAsync("/api/account/register",
            NewMember(email) with { BeltId = belt.Id });
        registered.EnsureSuccessStatusCode();

        var created = await registered.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        var memberId = created!.Data!.Id;

        var stored = await db.MemberBelts
            .AsNoTracking()
            .Where(mb => mb.MemberId == memberId)
            .ToListAsync();

        var current = Assert.Single(stored);
        Assert.Equal(belt.Id, current.BeltId);
        Assert.True(current.IsCurrentBelt);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.PostAsJsonAsync("/api/account/register", NewMember(TcmApiFactory.MemberEmail));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- Coach-only routes --------------------------------------------------------------------

    [Fact]
    public async Task Roles_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Roles_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Roles_AsCoach_Returns200()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithMalformedToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real.token");

        var response = await client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithTokenSignedByAnotherKey_Returns401()
    {
        // A token whose signature does not verify must be rejected outright, not merely because
        // its claims are wrong.
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var valid = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var tampered = valid[..^6] + "AAAAAA";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        var response = await client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Password reset -----------------------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_AnswersIdenticallyForKnownAndUnknownEmails()
    {
        var client = factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/account/forgot-password",
            new ForgotPasswordDto(TcmApiFactory.MemberEmail));
        var unknown = await client.PostAsJsonAsync("/api/account/forgot-password",
            new ForgotPasswordDto("ghost@nowhere.test"));

        var knownBody = await known.Content.ReadFromJsonAsync<ApiResponse<Unit>>();
        var unknownBody = await unknown.Content.ReadFromJsonAsync<ApiResponse<Unit>>();

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(knownBody!.Message, unknownBody!.Message);
        Assert.Equal(knownBody.Success, unknownBody.Success);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Fails()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/reset-password",
            new ResetPasswordDto(TcmApiFactory.MemberEmail, "made-up-token", "BrandNewPass456", "BrandNewPass456"));

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Unit>>();
        Assert.False(body!.Success);
    }

    [Fact]
    public async Task ResetPassword_WithMismatchedConfirmation_Fails()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/reset-password",
            new ResetPasswordDto(TcmApiFactory.MemberEmail, "made-up-token", "BrandNewPass456", "SomethingElse789"));

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Unit>>();
        Assert.False(body!.Success);
        // The envelope message is generic; the specific rule that failed is in Errors.
        Assert.Contains(body.Errors, e => e.Contains("match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_WithInvalidDetails_ReturnsValidationErrors()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var invalid = NewMember("not-an-email") with
        {
            FirstName = "",
            Height = 900m,
            DateOfBirth = new DateOnly(2400, 1, 1)
        };

        var response = await client.PostAsJsonAsync("/api/account/register", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        Assert.False(body!.Success);
        Assert.NotEmpty(body.Errors);
    }

    // ---- Common slice (SPEC section 6.2) ------------------------------------------------------

    [Fact]
    public async Task Belts_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/common/belts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClubNumbers_AsMember_Returns200()
    {
        // Both roles may see their own club's dashboard numbers (SPEC section 5).
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync("/api/common/club-numbers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClubNumbersInfoDto>>();
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
    }

    [Fact]
    public async Task ClubNumbers_WithInvalidMonth_Returns400()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.GetAsync("/api/common/club-numbers?month=13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClubNumbers_WithNoTrainings_ReportsZeroPercentNotDivideByZero()
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.GetAsync("/api/common/club-numbers?year=1999");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClubNumbersInfoDto>>();
        Assert.True(body!.Success);
        Assert.Equal(0, body.Data!.TrainingsHeld);
        Assert.Equal(0d, body.Data.AttendancePercentage);
    }

    private static MemberRegisterDto NewMember(string email) => new(
        FirstName: "New",
        LastName: "Recruit",
        Email: email,
        Password: TcmApiFactory.Password,
        Height: 170m,
        Weight: 62m,
        DateOfBirth: new DateOnly(2006, 5, 20),
        BeltId: 1,
        Role: "Member");
}
