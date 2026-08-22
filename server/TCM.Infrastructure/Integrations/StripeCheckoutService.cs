using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using TCM.Application.Abstractions;
using TCM.Application.Options;

namespace TCM.Infrastructure.Integrations;

/// <summary>
/// The real Stripe Checkout integration (SPEC section 3.2). Registered only when
/// <c>Stripe:Enabled</c> is true.
/// </summary>
/// <remarks>
/// Card data never reaches this application: the member is redirected to a Stripe-hosted page,
/// and all we ever see is a session id we then ask Stripe about.
/// </remarks>
public class StripeCheckoutService : ICheckoutService
{
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeCheckoutService> _logger;
    private readonly SessionService _sessions;

    public StripeCheckoutService(IOptions<StripeSettings> settings, ILogger<StripeCheckoutService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            throw new InvalidOperationException(
                "Stripe:Enabled is true but Stripe:SecretKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.SuccessUrl) || string.IsNullOrWhiteSpace(_settings.CancelUrl))
        {
            // These are per-environment values by resolved decision in SPEC section 9.
            throw new InvalidOperationException(
                "Stripe:SuccessUrl and Stripe:CancelUrl must be configured for this environment.");
        }

        _sessions = new SessionService(new StripeClient(_settings.SecretKey));
    }

    public bool IsLive => true;

    public async Task<CheckoutSession?> CreateSessionAsync(
        string memberId, string? stripeCustomerId, CancellationToken ct = default)
    {
        var successUrl = _settings.SuccessUrl.TrimEnd('/');
        var separator = successUrl.Contains('?') ? '&' : '?';

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            Customer = string.IsNullOrWhiteSpace(stripeCustomerId) ? null : stripeCustomerId,
            LineItems =
            [
                new SessionLineItemOptions { Price = _settings.MembershipPriceId, Quantity = 1 }
            ],
            // Stripe substitutes the real id, so the client never has to be trusted for it.
            SuccessUrl = $"{successUrl}{separator}session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = _settings.CancelUrl,
            ClientReferenceId = memberId
        };

        try
        {
            var session = await _sessions.CreateAsync(options, cancellationToken: ct);
            return new CheckoutSession(session.Id, session.Url);
        }
        catch (StripeException ex)
        {
            // Logged with detail; the caller returns a generic message to the member.
            _logger.LogError(ex, "Stripe rejected the checkout session for member {MemberId}.", memberId);
            return null;
        }
    }

    public async Task<CheckoutVerification> VerifyAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = await _sessions.GetAsync(sessionId, cancellationToken: ct);

            // "paid" is the only status that may result in a Payments row.
            var isPaid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);

            return new CheckoutVerification(isPaid, sessionId, session.ClientReferenceId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Could not verify Stripe session {SessionId}.", sessionId);
            return new CheckoutVerification(false, sessionId, null);
        }
    }
}
