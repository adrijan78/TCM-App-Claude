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
