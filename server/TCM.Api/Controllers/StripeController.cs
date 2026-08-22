using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Application.Dtos.Payments;
using TCM.Application.Services;

namespace TCM.Api.Controllers;

/// <summary>
/// Membership payment (SPEC sections 3.2 and 6.4). The client's only job is to redirect to the
/// URL this returns and then post the session id back; card data never touches our servers.
/// </summary>
[Authorize]
public class StripeController(IPaymentService paymentService) : BaseController
{
    /// <summary>Starts a payment for the signed-in member.</summary>
    [HttpPost("checkout-session")]
    public async Task<ActionResult<ApiResponse<CheckoutSessionDto>>> CreateCheckoutSession(CancellationToken ct)
        => HandleResult(await paymentService.StartCheckoutAsync(CallerId, ct));

    /// <summary>
    /// Called by the client after it returns from the payment page. The session is verified
    /// server-side before anything is recorded.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<ApiResponse<PaymentsDto>>> Confirm(
        [FromBody] ConfirmPaymentDto dto, CancellationToken ct)
        => HandleResult(await paymentService.ConfirmAsync(dto.SessionId, CallerId, ct));
}
