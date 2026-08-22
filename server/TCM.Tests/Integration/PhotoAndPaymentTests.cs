using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Dtos.Common;
using TCM.Application.Dtos.Payments;

namespace TCM.Tests.Integration;

/// <summary>
/// Photos stored in the database (decided 2026-08-22, superseding SPEC section 2's Firebase
/// choice) and the membership payment flow of SPEC section 3.2 running against the local fake.
/// </summary>
public class PhotoAndPaymentTests(TcmApiFactory factory) : IClassFixture<TcmApiFactory>
{
    /// <summary>A real 4x4 PNG — small, but genuinely a PNG down to the CRCs.</summary>
    private static readonly byte[] RealPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR4nGP4z8AARwzEcQCukw/x0F8jngAAAABJRU5ErkJggg==");

    private async Task<HttpClient> ClientAsAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/account/login", new LoginMemberDto(email, TcmApiFactory.Password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return client;
    }

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string declaredType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(declaredType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    // ---- Photos -------------------------------------------------------------------------------

    [Fact]
    public async Task UploadPhoto_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/photos/member/{factory.MemberId}", FileContent(RealPng, "a.png", "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_ForOwnProfile_ThenFetch_ReturnsIdenticalBytes()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var upload = await client.PostAsync(
            $"/api/photos/member/{factory.MemberId}", FileContent(RealPng, "me.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var body = await upload.Content.ReadFromJsonAsync<ApiResponse<PhotoDto>>();
        Assert.True(body!.Success);
        Assert.Equal("image/png", body.Data!.ContentType);

        var fetched = await client.GetAsync($"/api/photos/{body.Data.PublicId}");

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("image/png", fetched.Content.Headers.ContentType!.MediaType);
        Assert.Equal(RealPng, await fetched.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadPhoto_ForAnotherMember_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);

        var response = await client.PostAsync(
            $"/api/photos/member/{factory.MemberId}", FileContent(RealPng, "sneak.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_WithNonImageBytes_IsRejectedDespiteImageContentType()
    {
        // The declared content type says PNG and the extension says PNG. Only the bytes disagree,
        // and the bytes are the only thing that is not attacker-controlled.
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var notAnImage = Encoding.UTF8.GetBytes("MZ this is definitely not a picture, at all, ever.");

        var response = await client.PostAsync(
            $"/api/photos/member/{factory.MemberId}", FileContent(notAnImage, "payload.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PhotoDto>>();
        Assert.False(body!.Success);
    }

    [Fact]
    public async Task FetchPhoto_OfAnotherMember_AsMember_Returns403()
    {
        var owner = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var upload = await owner.PostAsync(
            $"/api/photos/member/{factory.MemberId}", FileContent(RealPng, "private.png", "image/png"));
        var uploaded = await upload.Content.ReadFromJsonAsync<ApiResponse<PhotoDto>>();

        var stranger = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);
        var response = await stranger.GetAsync($"/api/photos/{uploaded!.Data!.PublicId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FetchPhoto_OfMemberInOwnClub_AsCoach_Returns200()
    {
        var owner = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var upload = await owner.PostAsync(
            $"/api/photos/member/{factory.MemberId}", FileContent(RealPng, "forcoach.png", "image/png"));
        var uploaded = await upload.Content.ReadFromJsonAsync<ApiResponse<PhotoDto>>();

        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var response = await coach.GetAsync($"/api/photos/{uploaded!.Data!.PublicId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Payments (SPEC section 3.2) ------------------------------------------------------------

    [Fact]
    public async Task StartCheckout_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/stripe/checkout-session", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_WithUnknownSession_DoesNotRecordAPayment()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PostAsJsonAsync("/api/stripe/confirm",
            new ConfirmPaymentDto("local_never_issued_by_anyone"));

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentsDto>>();
        Assert.False(body!.Success);
    }

    [Fact]
    public async Task Confirm_IsIdempotent_ForTheSameSession()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var sessionId = await StartCheckoutAsync(client);

        var first = await client.PostAsJsonAsync("/api/stripe/confirm", new ConfirmPaymentDto(sessionId));
        var second = await client.PostAsJsonAsync("/api/stripe/confirm", new ConfirmPaymentDto(sessionId));

        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<PaymentsDto>>();
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponse<PaymentsDto>>();

        Assert.True(firstBody!.Success);
        Assert.True(secondBody!.Success);
        // Same row, not a second one: confirming twice must not charge or credit twice.
        Assert.Equal(firstBody.Data!.Id, secondBody.Data!.Id);
    }

    [Fact]
    public async Task Confirm_AnotherMembersSession_Returns403()
    {
        // Regression: the idempotency early-exit used to return the existing payment before the
        // ownership check ran, which leaked whose payment it was, when, and until when.
        var owner = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var sessionId = await StartCheckoutAsync(owner);

        await owner.PostAsJsonAsync("/api/stripe/confirm", new ConfirmPaymentDto(sessionId));

        var stranger = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);
        var response = await stranger.PostAsJsonAsync("/api/stripe/confirm", new ConfirmPaymentDto(sessionId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentsDto>>();
        Assert.False(body!.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task StartCheckout_WhileStripeDisabled_ReportsItIsNotLive()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PostAsync("/api/stripe/checkout-session", null);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionDto>>();
        Assert.True(body!.Success);
        // The client needs to know it is looking at the stand-in, not real Stripe.
        Assert.False(body.Data!.IsLiveStripe);
    }

    private static async Task<string> StartCheckoutAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/stripe/checkout-session", null);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionDto>>();
        var query = new Uri(body!.Data!.RedirectUrl).Query;
        return System.Web.HttpUtility.ParseQueryString(query)["session_id"]!;
    }
}
