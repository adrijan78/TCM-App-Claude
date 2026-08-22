namespace TCM.Domain.Entities;

/// <summary>
/// SPEC section 4: Payments. Two routes in — Stripe Checkout initiated by the member
/// (<see cref="IsPaidOnline"/> true) and a cash payment logged by the coach (false).
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public required string MemberId { get; set; }
    public ApplicationUser Member { get; set; } = null!;

    public bool IsPaidOnline { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    public DateOnly NextPaymentDate { get; set; }

    /// <summary>
    /// The Stripe Checkout Session this row was created from. Not in SPEC section 4, but
    /// required to implement section 3.2 safely: it is the idempotency key that stops a
    /// retried webhook or a refreshed success page writing a duplicate payment. Null for cash.
    /// </summary>
    public string? StripeSessionId { get; set; }
}
