using Microsoft.Extensions.Logging;
using TCM.Application.Abstractions;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Integrations;

/// <summary>
/// Used when Stripe is not configured. Returns no customer id, which registration treats as a
/// non-fatal outcome (see <c>IStripeCustomerService</c>) so members can still be added offline.
/// Phase 5 adds the real Stripe implementation.
/// </summary>
public class NoOpStripeCustomerService(ILogger<NoOpStripeCustomerService> logger) : IStripeCustomerService
{
    public Task<string?> CreateCustomerAsync(ApplicationUser user, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Stripe is not configured, so no customer was created for {MemberId}. " +
            "The id can be backfilled once keys are set.", user.Id);

        return Task.FromResult<string?>(null);
    }
}
