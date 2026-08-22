using TCM.Application.Dtos.Payments;
using TCM.Domain.Entities;

namespace TCM.Application.Abstractions;

/// <summary>SPEC section 3.1 names IPaymentRepository explicitly.</summary>
public interface IPaymentRepository : IRepository<Payment>
{
    /// <summary>The idempotency lookup behind SPEC section 3.2.</summary>
    Task<Payment?> GetByStripeSessionIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// The member's latest due date, so a renewal extends from it rather than from today when
    /// they pay early.
    /// </summary>
    Task<DateOnly?> GetLatestNextPaymentDateAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Inserts the payment unless one already exists for the same checkout session, in which
    /// case the existing row is returned and <c>Added</c> is false.
    /// </summary>
    /// <remarks>
    /// The race is resolved here rather than in the service so that the provider-specific
    /// unique-violation handling stays inside the data layer.
    /// </remarks>
    Task<(bool Added, Payment Payment)> AddIfSessionUnusedAsync(Payment payment, CancellationToken ct = default);

    /// <summary>
    /// The club-wide payments table of SPEC section 6.7, with its four filters applied in SQL.
    /// The club id comes from the caller's own account, never from the request.
    /// </summary>
    Task<IReadOnlyList<PaymentsDto>> GetClubHistoryAsync(
        int clubId, int? year, int? month, string? memberId, bool? isPaidOnline, CancellationToken ct = default);

    /// <summary>One member's payment history, newest first (SPEC section 6.4).</summary>
    Task<IReadOnlyList<PaymentsDto>> GetMemberHistoryAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// The payment with this id, but only if its member belongs to the given club. Tracked,
    /// because the one caller is the delete path. Null means "not there, or not yours" — the
    /// service must not distinguish the two to the client.
    /// </summary>
    Task<Payment?> FindInClubAsync(int id, int clubId, CancellationToken ct = default);
}
