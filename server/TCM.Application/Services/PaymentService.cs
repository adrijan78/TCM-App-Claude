using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Payments;
using TCM.Application.Options;
using TCM.Domain.Entities;

namespace TCM.Application.Services;

/// <summary>
/// Membership payments (SPEC sections 3.2 and 6.4). The rule this class exists to enforce: a
/// <c>Payments</c> row is written only after the provider confirms the session was paid.
/// </summary>
public class PaymentService(
    IPaymentRepository payments,
    ICheckoutService checkout,
    UserManager<ApplicationUser> userManager,
    IOptions<StripeSettings> settings,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<ApiResponse<CheckoutSessionDto>> StartCheckoutAsync(
        string callerId, CancellationToken ct = default)
    {
        var member = await userManager.FindByIdAsync(callerId);
        if (member is null)
        {
            return ApiResponse<CheckoutSessionDto>.Forbidden();
        }

        // A member pays their own membership. There is no "pay for someone else" route, so the
        // member id is never taken from the request.
        var session = await checkout.CreateSessionAsync(member.Id, member.StripeCustomerId, ct);
        if (session is null)
        {
            return ApiResponse<CheckoutSessionDto>.Fail(
                "Could not start the payment. Please try again shortly.", ErrorKind.External);
        }

        return ApiResponse<CheckoutSessionDto>.Ok(
            new CheckoutSessionDto(session.RedirectUrl, checkout.IsLive));
    }

    public async Task<ApiResponse<PaymentsDto>> ConfirmAsync(
        string sessionId, string callerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return ApiResponse<PaymentsDto>.Fail("No payment session was supplied.");
        }

        // Idempotency, first line: a refreshed success page or a retried webhook finds the row
        // that already exists instead of creating a second one.
        var existing = await payments.GetByStripeSessionIdAsync(sessionId, ct);
        if (existing is not null)
        {
            // Ownership is checked here too, not only on the create path. Returning the row
            // straight from this early exit would let anyone holding a session id read whose
            // payment it was, when and for how long — the same leak the create path refuses.
            if (existing.MemberId != callerId)
            {
                logger.LogWarning(
                    "Caller {CallerId} presented session {SessionId}, which belongs to {MemberId}.",
                    callerId, sessionId, existing.MemberId);
                return ApiResponse<PaymentsDto>.Forbidden();
            }

            return ApiResponse<PaymentsDto>.Ok(await ToDtoAsync(existing));
        }

        // The only thing that may cause a payment to be recorded. Arriving back at the success
        // URL proves nothing — anyone can navigate there.
        var verification = await checkout.VerifyAsync(sessionId, ct);
        if (!verification.IsPaid)
        {
            logger.LogWarning("Payment session {SessionId} did not verify as paid.", sessionId);
            return ApiResponse<PaymentsDto>.Fail("That payment has not completed.");
        }

        // The provider tells us whose payment it was. If the caller is someone else, refuse
        // rather than crediting the wrong member's membership.
        var memberId = verification.MemberId;
        if (string.IsNullOrWhiteSpace(memberId) || memberId != callerId)
        {
            logger.LogWarning(
                "Caller {CallerId} tried to confirm session {SessionId} belonging to {MemberId}.",
                callerId, sessionId, memberId ?? "(unknown)");
            return ApiResponse<PaymentsDto>.Forbidden();
        }

        var paidAt = DateTime.UtcNow;

        var payment = new Payment
        {
            MemberId = memberId,
            IsPaidOnline = true,
            PaymentDate = paidAt,
            NextPaymentDate = await CalculateNextDueDateAsync(memberId, paidAt, ct),
            StripeSessionId = sessionId
        };

        // Idempotency, second line: two confirmations racing each other. The unique filtered
        // index on StripeSessionId decides, and the loser gets back the row that won.
        var (added, stored) = await payments.AddIfSessionUnusedAsync(payment, ct);

        if (added)
        {
            logger.LogInformation("Recorded online payment {PaymentId} for member {MemberId}.", stored.Id, memberId);
        }

        return ApiResponse<PaymentsDto>.Ok(await ToDtoAsync(stored));
    }

    /// <summary>
    /// Extends from the member's current due date when they renew early, and from today when
    /// their membership has already lapsed. Kept here so the cash and online paths agree.
    /// </summary>
    private async Task<DateOnly> CalculateNextDueDateAsync(string memberId, DateTime paidAtUtc, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(paidAtUtc);
        var currentDue = await payments.GetLatestNextPaymentDateAsync(memberId, ct);

        var startFrom = currentDue is not null && currentDue > today ? currentDue.Value : today;
        return startFrom.AddDays(settings.Value.MembershipDays);
    }

    private async Task<PaymentsDto> ToDtoAsync(Payment payment)
    {
        var member = await userManager.FindByIdAsync(payment.MemberId);
        var fullName = member is null ? string.Empty : $"{member.FirstName} {member.LastName}";

        return new PaymentsDto(
            payment.Id,
            payment.MemberId,
            fullName,
            payment.IsPaidOnline,
            payment.PaymentDate,
            payment.NextPaymentDate);
    }
}
