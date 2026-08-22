using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
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
}
