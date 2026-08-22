using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCM.Application.Common;
using TCM.Application.Dtos.Payments;
using TCM.Application.Services;
using TCM.Domain.Constants;

namespace TCM.Api.Controllers;

/// <summary>
/// Membership payment records — the member's Membership tab (SPEC section 6.4) and the coach's
/// club-wide payments page (SPEC section 6.7). Starting and confirming an online payment lives
/// on <see cref="StripeController"/>; nothing here can record an online payment, because only a
/// verified checkout session may do that.
/// </summary>
[Authorize]
public class PaymentsController(IPaymentService paymentService) : BaseController
{
    /// <summary>All payments in the caller coach's own club, with the SPEC 6.7 filters.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PaymentsDto>>>> GetClubPayments(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? memberId,
        [FromQuery] PaymentMethod? method,
        CancellationToken ct)
        => HandleResult(await paymentService.GetClubPaymentsAsync(
            CallerId, IsCoach, year, month, memberId, method, ct));

    /// <summary>
    /// One member's history and next-due-date banner. Open to both roles by attribute; the
    /// service decides whether this particular caller may see this particular member.
    /// </summary>
    [HttpGet("member/{memberId}")]
    public async Task<ActionResult<ApiResponse<MemberPaymentHistoryDto>>> GetMemberHistory(
        string memberId, CancellationToken ct)
        => HandleResult(await paymentService.GetMemberHistoryAsync(memberId, CallerId, IsCoach, ct));

    /// <summary>Logs a cash payment taken by the coach.</summary>
    [HttpPost("cash")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<PaymentsDto>>> LogCashPayment(
        [FromBody] CashPaymentDto dto, CancellationToken ct)
        => HandleResult(await paymentService.RecordCashPaymentAsync(dto, CallerId, IsCoach, ct));

    /// <summary>Deletes a payment record. The confirmation modal is the client's job.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Coach)]
    public async Task<ActionResult<ApiResponse<Unit>>> Delete(int id, CancellationToken ct)
        => HandleResult(await paymentService.DeleteAsync(id, CallerId, IsCoach, ct));
}
