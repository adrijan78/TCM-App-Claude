namespace TCM.Application.Options;

/// <summary>Bound from the "Stripe" configuration section (SPEC section 3.2).</summary>
public class StripeSettings
{
    public const string SectionName = "Stripe";

    /// <summary>
    /// False until real Stripe keys are in place (decided 2026-08-22). While false the app
    /// registers a local fake so the whole membership-payment flow still works end to end.
    /// Startup logs a warning whenever this is off, so a deployment cannot ship the fake quietly.
    /// </summary>
    public bool Enabled { get; set; }

    public string SecretKey { get; set; } = string.Empty;
    public string MembershipPriceId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Per-environment, never hardcoded (SPEC section 9, resolved decision).</summary>
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;

    /// <summary>How long a membership lasts. Drives Payments.NextPaymentDate.</summary>
    public int MembershipDays { get; set; } = 30;

    /// <summary>Shown on the client so the member knows what they are about to pay.</summary>
    public decimal MembershipAmount { get; set; } = 20m;
    public string Currency { get; set; } = "eur";
}
