using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using TCM.Application.Abstractions;
using TCM.Application.Options;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Integrations;

/// <summary>
/// Creates the Stripe Customer at member registration (SPEC section 3.2). Registered only when
/// <c>Stripe:Enabled</c> is true; otherwise <see cref="NoOpStripeCustomerService"/> stands in.
/// </summary>
public class StripeCustomerService(
    IOptions<StripeSettings> settings,
    ILogger<StripeCustomerService> logger) : IStripeCustomerService
{
    private readonly CustomerService _customers = new(new StripeClient(settings.Value.SecretKey));

    public async Task<string?> CreateCustomerAsync(ApplicationUser user, CancellationToken ct = default)
    {
        try
        {
            var customer = await _customers.CreateAsync(new CustomerCreateOptions
            {
                Email = user.Email,
                Name = $"{user.FirstName} {user.LastName}",
                Metadata = new Dictionary<string, string> { ["memberId"] = user.Id }
            }, cancellationToken: ct);

            return customer.Id;
        }
        catch (StripeException ex)
        {
            // Never fatal: a coach must not be blocked from adding a member by a Stripe outage.
            // The id can be backfilled later.
            logger.LogError(ex, "Could not create a Stripe customer for member {MemberId}.", user.Id);
            return null;
        }
    }
}
