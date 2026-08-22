using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCM.Application.Abstractions;
using TCM.Application.Options;

namespace TCM.Infrastructure.Integrations;

/// <summary>
/// Stands in for Stripe while <c>Stripe:Enabled</c> is false, so the membership-payment flow
/// works end to end with no Stripe account (decided 2026-08-22 — a working app first, real
/// Stripe after).
/// </summary>
/// <remarks>
/// This deliberately keeps the same shape as the real thing rather than short-circuiting it:
/// a session is created, the member is redirected, and the payment is only recorded after
/// <see cref="VerifyAsync"/> confirms that session exists. Nothing here writes a payment from a
/// bare redirect, because the real implementation must not either and the two paths share the
/// service that records it.
///
/// Sessions are held in memory, so a restart forgets any that are mid-flight. That is fine for
/// a stand-in and is one more reason it must not run in production.
/// </remarks>
public class FakeCheckoutService(
    IOptions<StripeSettings> settings,
    ILogger<FakeCheckoutService> logger) : ICheckoutService
{
    private static readonly ConcurrentDictionary<string, string> Sessions = new();

    public bool IsLive => false;

    public Task<CheckoutSession?> CreateSessionAsync(
        string memberId, string? stripeCustomerId, CancellationToken ct = default)
    {
        var sessionId = $"local_{Guid.NewGuid():N}";
        Sessions[sessionId] = memberId;

        var successUrl = settings.Value.SuccessUrl.TrimEnd('/');
        var separator = successUrl.Contains('?') ? '&' : '?';
        var redirectUrl = $"{successUrl}{separator}session_id={sessionId}";

        logger.LogWarning(
            "Stripe is disabled: issued LOCAL FAKE checkout session {SessionId} for member {MemberId}. " +
            "No money moves. Set Stripe:Enabled=true with real keys to use Stripe Checkout.",
            sessionId, memberId);

        return Task.FromResult<CheckoutSession?>(new CheckoutSession(sessionId, redirectUrl));
    }

    public Task<CheckoutVerification> VerifyAsync(string sessionId, CancellationToken ct = default)
    {
        // Still a real check: an id this service never issued does not verify.
        if (!Sessions.TryGetValue(sessionId, out var memberId))
        {
            logger.LogWarning("Unknown fake checkout session {SessionId} presented for verification.", sessionId);
            return Task.FromResult(new CheckoutVerification(false, sessionId, null));
        }

        return Task.FromResult(new CheckoutVerification(true, sessionId, memberId));
    }
}
