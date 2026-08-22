using TCM.Application.Common;
using TCM.Application.Dtos.Payments;

namespace TCM.Application.Services;

public interface IPaymentService
{
    /// <summary>Starts a membership payment for the caller themselves (SPEC section 6.4).</summary>
    Task<ApiResponse<CheckoutSessionDto>> StartCheckoutAsync(string callerId, CancellationToken ct = default);

    /// <summary>
    /// Records the payment, but only after the provider confirms the session was paid. Safe to
    /// call more than once with the same session — the second call returns the existing row.
    /// </summary>
    Task<ApiResponse<PaymentsDto>> ConfirmAsync(string sessionId, string callerId, CancellationToken ct = default);

    /// <summary>
    /// The club-wide payments table (SPEC section 6.7). Coach only, and always scoped to the
    /// caller coach's own club — the club is never taken from the request.
    /// </summary>
    Task<ApiResponse<IReadOnlyList<PaymentsDto>>> GetClubPaymentsAsync(
        string callerId, bool isCoach, int? year, int? month, string? memberId, PaymentMethod? method,
        CancellationToken ct = default);

    /// <summary>
    /// One member's history plus the next-due-date banner (SPEC section 6.4). A coach may read
    /// anyone in their own club; a member may read only themselves.
    /// </summary>
    Task<ApiResponse<MemberPaymentHistoryDto>> GetMemberHistoryAsync(
        string memberId, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>
    /// Logs a cash payment for a member (SPEC section 5, coach only). Uses the same next-due-date
    /// rule as the online path, so the two can never disagree.
    /// </summary>
    Task<ApiResponse<PaymentsDto>> RecordCashPaymentAsync(
        CashPaymentDto dto, string callerId, bool isCoach, CancellationToken ct = default);

    /// <summary>
    /// Deletes a payment record (SPEC sections 6.4 and 6.7). Coach only, scoped to their club;
    /// the confirmation dialog is the client's job.
    /// </summary>
    Task<ApiResponse<Unit>> DeleteAsync(int id, string callerId, bool isCoach, CancellationToken ct = default);
}
