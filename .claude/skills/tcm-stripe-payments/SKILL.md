---
name: tcm-stripe-payments
description: The TCM membership payment flow — Stripe Checkout Session creation, customer creation at registration, environment-based success and cancel URLs, server-side verification before recording a payment, cash payments logged by the coach, and the payment history screens. Use for any work on StripeController, StripeService, Payments, or the pay-membership UI.
---

# TCM payments

Spec sections 3.2, 6.4 and 6.7. Two payment methods exist: **Online** (Stripe, initiated by the member) and **Cash** (logged by the coach).

## The Stripe flow

1. Member clicks "Pay Membership Fee".
2. Angular calls `POST /api/stripe/checkout-session`.
3. `StripeService` creates the session and returns `session.Url`:

```csharp
var options = new SessionCreateOptions
{
    Mode       = "payment",
    Customer   = user.StripeCustomerId,
    LineItems  = new List<SessionLineItemOptions> { new() { Price = _settings.MembershipPriceId, Quantity = 1 } },
    SuccessUrl = _settings.SuccessUrl,   // configuration, per environment
    CancelUrl  = _settings.CancelUrl,    // configuration, per environment
    ClientReferenceId = user.Id
};
```

4. Angular does `window.location.href = session.Url`. Nothing else.
5. Stripe redirects the browser back to the configured success or cancel URL.
6. **The server verifies before recording.** Retrieve the session (or handle `checkout.session.completed` via webhook), confirm `PaymentStatus == "paid"`, then write the `Payments` row with `IsPaidOnline = true`, `PaymentDate` and the computed `NextPaymentDate`.

## Rules you must not break

- **Never record a payment because the browser came back to the success URL.** A user can navigate there directly. Only server-side session status or a signature-verified webhook proves payment.
- **`SuccessUrl` and `CancelUrl` are configuration**, not constants. This is a resolved decision in spec section 9 — the original thesis hardcoded `http://localhost:4200/...` and that must not come back.
- **No card data anywhere** in our request bodies, responses, logs or database. Stripe's hosted page handles all of it.
- **Idempotency.** A retried webhook or a refreshed success page must not create a duplicate `Payments` row. Key on the Stripe session or payment-intent id.
- **Secret key from configuration**, test mode during development. Webhook signing secret likewise, and verify the signature on every webhook call.

## Customer creation

At coach-driven registration (spec section 6.1), create a Stripe Customer with the member's name and email, and persist `StripeCustomerId` on `AspNetUsers`. If Stripe fails, the registration should still succeed — log it and allow the id to be backfilled, rather than blocking a coach from adding a member.

## Cash payments

Coach-only endpoint writing `IsPaidOnline = false` with the coach-supplied date. Same `NextPaymentDate` calculation as online. Keep that calculation in one place so both paths agree.

## Screens

- **Member profile, Membership tab (6.4):** banner with the next due date, history table of date / method / due date, delete a record.
- **Club-wide payments (6.7), coach only:** all payments with filters by year, month, member and method; delete with a confirmation modal.

## Tooling

The `stripe` plugin: `stripe-best-practices` before designing the flow, `stripe-docs` for API details, `test-cards` for the numbers to test with, `explain-error` when an API call fails. The `stripe` MCP server can answer live account questions. Test-mode keys only.
