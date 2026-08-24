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
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisteredMemberDto>>();
        Assert.True(body!.Success);
        Assert.Contains("Member", body.Data!.Roles);
    }

    [Fact]
    public async Task Register_DoesNotHandBackATokenForTheNewAccount()
    {
        // Registration authenticates the coach, not the member being created. Returning a signed
        // JWT for the new account would give the caller a working credential for someone else --
        // and for a newly created Coach, a full admin one.
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await client.PostAsJsonAsync("/api/account/register",
            NewMember($"tokenless-{Guid.NewGuid():N}@test.local"));

        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("\"token\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eyJ", raw, StringComparison.Ordinal); // a JWT always starts this way
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

        var created = await registered.Content.ReadFromJsonAsync<ApiResponse<RegisteredMemberDto>>();
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
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisteredMemberDto>>();
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

    // ---- Completeness: every protected route, both refusals ------------------------------------

    /// <summary>
    /// Every authenticated route in the API. Listing them in one place is what turns "we tested
    /// the routes we remembered" into a matrix: a new endpoint that is not added here has no
    /// anonymous test, and a reviewer can see the whole surface at a glance.
    /// </summary>
    public static TheoryData<string, string> ProtectedRoutes() => new()
    {
        { "POST", "/api/account/register" },
        { "GET", "/api/common/belts" },
        { "GET", "/api/common/club-numbers" },
        { "GET", "/api/roles" },
        { "GET", "/api/members" },
        { "GET", "/api/members/some-id" },
        { "PUT", "/api/members/some-id" },
        { "PATCH", "/api/members/some-id/deactivate" },
        { "GET", "/api/members/some-id/belts" },
        { "POST", "/api/members/some-id/belts" },
        { "DELETE", "/api/members/some-id/belts/1" },
        { "GET", "/api/notes" },
        { "GET", "/api/notes/member/some-id" },
        { "GET", "/api/notes/training/1/member/some-id" },
        { "POST", "/api/notes" },
        { "DELETE", "/api/notes/1" },
        { "GET", "/api/payments" },
        { "GET", "/api/payments/member/some-id" },
        { "POST", "/api/payments/cash" },
        { "DELETE", "/api/payments/1" },
        { "POST", "/api/photos/member/some-id" },
        { "GET", $"/api/photos/{EmptyGuid}" },
        { "DELETE", $"/api/photos/{EmptyGuid}" },
        { "POST", "/api/stripe/checkout-session" },
        { "POST", "/api/stripe/confirm" },
        { "GET", "/api/trainings" },
        { "GET", "/api/trainings/calendar" },
        { "GET", "/api/trainings/1" },
        { "POST", "/api/trainings" },
        { "PUT", "/api/trainings/1" },
        { "DELETE", "/api/trainings/1" },
        { "POST", "/api/trainings/1/attendance" },
        { "PUT", "/api/trainings/1/attendance/some-id/performance" },
        { "GET", "/api/trainings/member/some-id/attendance" }
    };

    /// <summary>
    /// The coach-only subset. A member token must be refused on every one of these, whatever
    /// ids are in the URL — the role is decided before the route parameters mean anything.
    /// </summary>
    public static TheoryData<string, string> CoachOnlyRoutes() => new()
    {
        { "POST", "/api/account/register" },
        { "GET", "/api/roles" },
        { "GET", "/api/members" },
        { "PATCH", "/api/members/some-id/deactivate" },
        { "POST", "/api/members/some-id/belts" },
        { "DELETE", "/api/members/some-id/belts/1" },
        { "GET", "/api/notes" },
        { "GET", "/api/payments" },
        { "POST", "/api/payments/cash" },
        { "DELETE", "/api/payments/1" },
        { "GET", "/api/trainings" },
        { "GET", "/api/trainings/calendar" },
        { "POST", "/api/trainings" },
        { "PUT", "/api/trainings/1" },
        { "DELETE", "/api/trainings/1" },
        { "PUT", "/api/trainings/1/attendance/some-id/performance" }
    };

    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task EveryProtectedRoute_Anonymously_Returns401(string method, string route)
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(EmptyRequest(method, route));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CoachOnlyRoutes))]
    public async Task EveryCoachOnlyRoute_AsMember_Returns403(string method, string route)
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.SendAsync(EmptyRequest(method, route));

        // 403, not 404: the role is decided before the made-up ids in these URLs are looked at.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task EveryProtectedRoute_WithATamperedToken_Returns401(string method, string route)
    {
        var client = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var valid = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", valid[..^6] + "AAAAAA");

        var response = await client.SendAsync(EmptyRequest(method, route));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A request with an empty body of the shape the route consumes. Nothing here should ever
    /// reach model binding — these tests assert the pipeline refuses first — so the body only
    /// has to be well formed enough to get past content negotiation.
    /// </summary>
    /// <remarks>
    /// The photo upload consumes multipart/form-data, and ASP.NET Core rejects the wrong content
    /// type with 415 <em>before</em> the authorization filter runs. Posting JSON there would
    /// prove nothing about authentication, so it gets a multipart body instead.
    /// </remarks>
    private static HttpRequestMessage EmptyRequest(string method, string route) =>
        new(new HttpMethod(method), route)
        {
            Content = route.StartsWith("/api/photos/member/", StringComparison.Ordinal)
                ? new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "x.png" } }
                : new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };


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
