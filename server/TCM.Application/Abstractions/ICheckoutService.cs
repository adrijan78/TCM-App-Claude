namespace TCM.Application.Abstractions;

/// <summary>Where to send the member's browser, and the session that will be verified later.</summary>
public record CheckoutSession(string SessionId, string RedirectUrl);

/// <summary>The outcome of asking the provider whether a session was actually paid.</summary>
public record CheckoutVerification(bool IsPaid, string SessionId, string? MemberId);

/// <summary>
/// Creating and verifying a membership payment (SPEC section 3.2). Two implementations exist:
/// the real Stripe one, and a local fake used while <c>Stripe:Enabled</c> is false.
/// </summary>
/// <remarks>
/// Both implementations must genuinely verify. A payment row is written only after
/// <see cref="VerifyAsync"/> confirms the session was paid — never on the strength of the
/// browser arriving back at the success URL, which anyone can navigate to directly.
/// </remarks>
public interface ICheckoutService
{
    bool IsLive { get; }

    Task<CheckoutSession?> CreateSessionAsync(string memberId, string? stripeCustomerId, CancellationToken ct = default);

    Task<CheckoutVerification> VerifyAsync(string sessionId, CancellationToken ct = default);
}
