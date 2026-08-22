namespace TCM.Application.Dtos.Payments;

/// <summary>SPEC section 3.1 — PaymentsDto. One row of the payment history (sections 6.4, 6.7).</summary>
public record PaymentsDto(
    int Id,
    string MemberId,
    string MemberFullName,
    bool IsPaidOnline,
    DateTime PaymentDate,
    DateOnly NextPaymentDate);

/// <summary>Where to send the member's browser to pay (SPEC section 3.2).</summary>
public record CheckoutSessionDto(string RedirectUrl, bool IsLiveStripe);

/// <summary>What the client posts back after returning from the payment page.</summary>
public record ConfirmPaymentDto(string SessionId);

/// <summary>
/// How a membership was paid, as the club-wide filter of SPEC section 6.7 spells it. Bound from
/// the query string, so it lives here rather than in the domain — the stored column is the
/// boolean <c>Payments.IsPaidOnline</c> and stays that way.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Online = 1
}

/// <summary>What the coach posts to log a cash payment (SPEC section 5, coach only).</summary>
/// <param name="MemberId">The member being credited. Never the caller — a coach pays for nobody.</param>
/// <param name="PaymentDate">
/// When the cash was handed over. Optional: omitted means now. Interpreted as UTC, because
/// <c>Payments.PaymentDate</c> is stored as UTC.
/// </param>
public record CashPaymentDto(string MemberId, DateTime? PaymentDate);

/// <summary>The next-due-date banner at the top of the Membership tab (SPEC section 6.4).</summary>
/// <param name="NextPaymentDate">Null when the member has never paid.</param>
/// <param name="IsOverdue">True when the due date has passed, and when there has never been one.</param>
/// <param name="DaysUntilDue">Negative once overdue, null when the member has never paid.</param>
public record MembershipStatusDto(DateOnly? NextPaymentDate, bool IsOverdue, int? DaysUntilDue);

/// <summary>One member's payment history plus the banner above it (SPEC section 6.4).</summary>
public record MemberPaymentHistoryDto(
    string MemberId,
    string MemberFullName,
    MembershipStatusDto Membership,
    IReadOnlyList<PaymentsDto> Payments);
