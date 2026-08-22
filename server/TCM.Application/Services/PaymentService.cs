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

    public async Task<ApiResponse<IReadOnlyList<PaymentsDto>>> GetClubPaymentsAsync(
        string callerId, bool isCoach, int? year, int? month, string? memberId, PaymentMethod? method,
        CancellationToken ct = default)
    {
        // The attribute on the controller says this too. Repeated here because the rule belongs
        // to the service, not to whichever controller happens to call it.
        if (!isCoach)
        {
            return ApiResponse<IReadOnlyList<PaymentsDto>>.Forbidden();
        }

        if (month is < 1 or > 12)
        {
            return ApiResponse<IReadOnlyList<PaymentsDto>>.Fail("Month must be between 1 and 12.");
        }

        if (year is < 2000 or > 2100)
        {
            return ApiResponse<IReadOnlyList<PaymentsDto>>.Fail("Year must be between 2000 and 2100.");
        }

        var coach = await userManager.FindByIdAsync(callerId);
        if (coach?.ClubId is null)
        {
            return ApiResponse<IReadOnlyList<PaymentsDto>>.Forbidden();
        }

        var isPaidOnline = method switch
        {
            PaymentMethod.Online => true,
            PaymentMethod.Cash => false,
            _ => (bool?)null
        };

        // The club is the caller's own, taken from their account. A club id in the query string
        // would let one coach read another club's takings.
        var rows = await payments.GetClubHistoryAsync(
            coach.ClubId.Value, year, month, Blank(memberId) ? null : memberId, isPaidOnline, ct);

        return ApiResponse<IReadOnlyList<PaymentsDto>>.Ok(rows);
    }

    public async Task<ApiResponse<MemberPaymentHistoryDto>> GetMemberHistoryAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct = default)
    {
        if (Blank(memberId))
        {
            return ApiResponse<MemberPaymentHistoryDto>.Fail("No member was supplied.");
        }

        // The whole point of this check: a member who edits the id in the URL gets nothing.
        if (!isCoach && memberId != callerId)
        {
            return ApiResponse<MemberPaymentHistoryDto>.Forbidden();
        }

        var member = await userManager.FindByIdAsync(memberId);
        if (member is null)
        {
            return ApiResponse<MemberPaymentHistoryDto>.NotFound("Member not found.");
        }

        if (isCoach && !await InSameClubAsync(callerId, member.ClubId))
        {
            return ApiResponse<MemberPaymentHistoryDto>.Forbidden();
        }

        var history = await payments.GetMemberHistoryAsync(memberId, ct);

        // Max over the rows already fetched rather than a second round trip. The latest due date
        // is not always the newest payment's — a back-dated cash entry can sit in between.
        DateOnly? nextDue = history.Count == 0 ? null : history.Max(p => p.NextPaymentDate);

        return ApiResponse<MemberPaymentHistoryDto>.Ok(new MemberPaymentHistoryDto(
            member.Id,
            $"{member.FirstName} {member.LastName}",
            BuildStatus(nextDue),
            history));
    }

    public async Task<ApiResponse<PaymentsDto>> RecordCashPaymentAsync(
        CashPaymentDto dto, string callerId, bool isCoach, CancellationToken ct = default)
    {
        if (!isCoach)
        {
            return ApiResponse<PaymentsDto>.Forbidden();
        }

        if (dto is null || Blank(dto.MemberId))
        {
            return ApiResponse<PaymentsDto>.Fail("Choose the member this payment is for.");
        }

        var coach = await userManager.FindByIdAsync(callerId);
        if (coach?.ClubId is null)
        {
            return ApiResponse<PaymentsDto>.Forbidden();
        }

        var member = await userManager.FindByIdAsync(dto.MemberId);
        if (member is null)
        {
            return ApiResponse<PaymentsDto>.NotFound("Member not found.");
        }

        if (member.ClubId != coach.ClubId)
        {
            return ApiResponse<PaymentsDto>.Forbidden();
        }

        var now = DateTime.UtcNow;
        var paidAt = ToUtc(dto.PaymentDate) ?? now;

        // A small tolerance, because a client clock can run a little ahead of the server's.
        if (paidAt > now.AddMinutes(5))
        {
            return ApiResponse<PaymentsDto>.Fail("A payment cannot be dated in the future.");
        }

        if (DateOnly.FromDateTime(paidAt) < member.StartedOn)
        {
            return ApiResponse<PaymentsDto>.Fail("A payment cannot be dated before the member joined the club.");
        }

        var payment = new Payment
        {
            MemberId = member.Id,
            IsPaidOnline = false,
            PaymentDate = paidAt,
            // The same call the online path makes. One rule, one implementation.
            NextPaymentDate = await CalculateNextDueDateAsync(member.Id, paidAt, ct),
            StripeSessionId = null
        };

        await payments.AddAsync(payment, ct);
        await payments.SaveChangesAsync(ct);

        logger.LogInformation(
            "Coach {CoachId} logged cash payment {PaymentId} for member {MemberId}.",
            callerId, payment.Id, member.Id);

        return ApiResponse<PaymentsDto>.Ok(await ToDtoAsync(payment));
    }

    public async Task<ApiResponse<Unit>> DeleteAsync(
        int id, string callerId, bool isCoach, CancellationToken ct = default)
    {
        if (!isCoach)
        {
            return ApiResponse.Forbidden();
        }

        var coach = await userManager.FindByIdAsync(callerId);
        if (coach?.ClubId is null)
        {
            return ApiResponse.Forbidden();
        }

        // Scoped to the coach's own club: a payment belonging to another club is simply not found.
        var payment = await payments.FindInClubAsync(id, coach.ClubId.Value, ct);
        if (payment is null)
        {
            return ApiResponse.NotFound("Payment not found.");
        }

        payments.Remove(payment);
        await payments.SaveChangesAsync(ct);

        logger.LogInformation("Coach {CoachId} deleted payment {PaymentId}.", callerId, id);

        return ApiResponse.Ok("Payment deleted.");
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

    /// <summary>The banner at the top of the Membership tab (SPEC section 6.4).</summary>
    private static MembershipStatusDto BuildStatus(DateOnly? nextDue)
    {
        if (nextDue is null)
        {
            // Never paid: there is no date to show and nothing is running out — but the banner
            // should still read as "not up to date".
            return new MembershipStatusDto(null, true, null);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var days = nextDue.Value.DayNumber - today.DayNumber;

        return new MembershipStatusDto(nextDue, nextDue.Value < today, days);
    }

    private async Task<bool> InSameClubAsync(string callerId, int? clubId)
    {
        var caller = await userManager.FindByIdAsync(callerId);
        return caller?.ClubId is not null && caller.ClubId == clubId;
    }

    /// <summary>
    /// <c>Payments.PaymentDate</c> is stored as UTC. A date posted without an offset is taken as
    /// UTC rather than as the server's local time, so the stored value does not depend on where
    /// the API happens to run.
    /// </summary>
    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
