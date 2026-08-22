using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Content).HasMaxLength(4000).IsRequired();
        builder.Property(n => n.Priority).HasConversion<int>();

        // Two foreign keys into AspNetUsers. Both must be Restrict — cascading either one gives
        // SQL Server two delete paths into Notes and the migration is rejected outright.
        builder.HasOne(n => n.FromMember)
            .WithMany()
            .HasForeignKey(n => n.FromMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.ToMember)
            .WithMany()
            .HasForeignKey(n => n.ToMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Training)
            .WithMany(t => t.Notes)
            .HasForeignKey(n => n.TrainingId)
            .OnDelete(DeleteBehavior.SetNull);

        // The member profile lists notes High priority first, newest first (SPEC section 6.8).
        builder.HasIndex(n => new { n.ToMemberId, n.Priority, n.CreatedAt });
        builder.HasIndex(n => n.TrainingId);
    }
}
