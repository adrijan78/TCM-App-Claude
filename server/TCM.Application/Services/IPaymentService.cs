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
}
