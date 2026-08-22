using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
using TCM.Application.Dtos.Payments;
using TCM.Domain.Entities;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure.Repositories;

public class PaymentRepository(ApplicationDbContext context) : Repository<Payment>(context), IPaymentRepository
{
    public async Task<Payment?> GetByStripeSessionIdAsync(string sessionId, CancellationToken ct = default) =>
        await Context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StripeSessionId == sessionId, ct);

    public async Task<(bool Added, Payment Payment)> AddIfSessionUnusedAsync(
        Payment payment, CancellationToken ct = default)
    {
        var existing = await GetByStripeSessionIdAsync(payment.StripeSessionId!, ct);
        if (existing is not null)
        {
            return (false, existing);
        }

        try
        {
            await AddAsync(payment, ct);
            await SaveChangesAsync(ct);
            return (true, payment);
        }
        catch (DbUpdateException)
        {
            // Two confirmations raced. The unique filtered index on StripeSessionId decided;
            // the loser returns the row that won rather than surfacing an error to the member.
            Context.Entry(payment).State = EntityState.Detached;

            var winner = await GetByStripeSessionIdAsync(payment.StripeSessionId!, ct);
            if (winner is null) throw;

            return (false, winner);
        }
    }

    public async Task<DateOnly?> GetLatestNextPaymentDateAsync(string memberId, CancellationToken ct = default) =>
        await Context.Payments
            .AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.NextPaymentDate)
            .Select(p => (DateOnly?)p.NextPaymentDate)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PaymentsDto>> GetClubHistoryAsync(
        int clubId, int? year, int? month, string? memberId, bool? isPaidOnline, CancellationToken ct = default) =>
        // Every filter is a Where on the IQueryable, so all four reach SQL. Nothing is
        // materialised and then filtered in memory.
        await Context.Payments
            .AsNoTracking()
            .Where(p => p.Member.ClubId == clubId)
            .Where(p => year == null || p.PaymentDate.Year == year)
            .Where(p => month == null || p.PaymentDate.Month == month)
            .Where(p => memberId == null || p.MemberId == memberId)
            .Where(p => isPaidOnline == null || p.IsPaidOnline == isPaidOnline)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .Select(p => new PaymentsDto(
                p.Id,
                p.MemberId,
                p.Member.FirstName + " " + p.Member.LastName,
                p.IsPaidOnline,
                p.PaymentDate,
                p.NextPaymentDate))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PaymentsDto>> GetMemberHistoryAsync(
        string memberId, CancellationToken ct = default) =>
        await Context.Payments
            .AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .Select(p => new PaymentsDto(
                p.Id,
                p.MemberId,
                p.Member.FirstName + " " + p.Member.LastName,
                p.IsPaidOnline,
                p.PaymentDate,
                p.NextPaymentDate))
            .ToListAsync(ct);

    /// <summary>Tracked on purpose: the row is about to be deleted.</summary>
    public async Task<Payment?> FindInClubAsync(int id, int clubId, CancellationToken ct = default) =>
        await Context.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.Member.ClubId == clubId, ct);
}
