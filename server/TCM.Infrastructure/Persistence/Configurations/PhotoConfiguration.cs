using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.Property(p => p.Url).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.PublicId).HasMaxLength(500).IsRequired();

        // The other half of the AspNetUsers <-> Photos cycle. Restrict on both sides.
        builder.HasOne(p => p.Member)
            .WithMany()
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.MemberId);
    }
}
