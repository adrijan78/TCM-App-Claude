using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.StripeCustomerId).HasMaxLength(255);

        // Explicit precision, or SQL Server picks decimal(18,2) and EF warns about it.
        builder.Property(u => u.Height).HasPrecision(5, 2);
        builder.Property(u => u.Weight).HasPrecision(5, 2);

        builder.HasOne(u => u.Club)
            .WithMany(c => c.Members)
            .HasForeignKey(u => u.ClubId)
            .OnDelete(DeleteBehavior.Restrict);

        // AspNetUsers.PhotoId and Photos.MemberId point at each other, so this pair forms a
        // cycle. Both sides are optional and neither cascades, which lets SQL Server accept it.
        builder.HasOne(u => u.Photo)
            .WithMany()
            .HasForeignKey(u => u.PhotoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.ClubId);
        builder.HasIndex(u => u.IsActive);
    }
}
