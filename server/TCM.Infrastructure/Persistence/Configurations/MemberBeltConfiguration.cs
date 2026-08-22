using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class MemberBeltConfiguration : IEntityTypeConfiguration<MemberBelt>
{
    public void Configure(EntityTypeBuilder<MemberBelt> builder)
    {
        builder.Property(mb => mb.Description).HasMaxLength(500);

        builder.HasOne(mb => mb.Member)
            .WithMany(u => u.Belts)
            .HasForeignKey(mb => mb.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mb => mb.Belt)
            .WithMany(b => b.MemberBelts)
            .HasForeignKey(mb => mb.BeltId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(mb => mb.MemberId);

        // A member has at most one current belt (SPEC section 4). Enforced in the database as
        // well as the service layer, because a concurrent promotion could otherwise create two.
        // Uses the named-index overload: calling HasIndex twice on the same property returns
        // the *same* index builder, which would silently redefine the plain index above.
        builder.HasIndex(mb => mb.MemberId, "IX_MemberBelts_MemberId_CurrentBelt")
            .IsUnique()
            .HasFilter("[IsCurrentBelt] = 1");
    }
}
