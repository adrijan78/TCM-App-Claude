using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;

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
        Assert.Contains("match", body.Message, StringComparison.OrdinalIgnoreCase);
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
