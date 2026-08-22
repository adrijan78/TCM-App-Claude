using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.StripeSessionId).HasMaxLength(255);

        builder.HasOne(p => p.Member)
            .WithMany(u => u.Payments)
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.MemberId);
        builder.HasIndex(p => p.PaymentDate);

        // The idempotency guarantee behind SPEC section 3.2: a retried webhook or a refreshed
        // success page cannot produce a second row for the same Checkout Session. Filtered so
        // the many cash payments (null session id) do not collide with each other.
        builder.HasIndex(p => p.StripeSessionId)
            .IsUnique()
            .HasFilter("[StripeSessionId] IS NOT NULL");
    }
}
