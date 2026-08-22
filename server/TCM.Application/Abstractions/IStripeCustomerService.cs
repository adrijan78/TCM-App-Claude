using TCM.Domain.Entities;

namespace TCM.Application.Abstractions;

/// <summary>
/// Creates the Stripe Customer that SPEC section 3.2 requires at member registration. Split out
/// from the wider payment service so registration depends on this one narrow capability.
/// </summary>
/// <remarks>
/// Returns null when Stripe is not configured or the call failed. Registration must still
/// succeed in that case — a coach cannot be blocked from adding a member by a Stripe outage —
/// and the id can be backfilled later.
/// </remarks>
public interface IStripeCustomerService
{
    Task<string?> CreateCustomerAsync(ApplicationUser user, CancellationToken ct = default);
}
