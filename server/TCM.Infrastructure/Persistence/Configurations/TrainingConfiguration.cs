using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class TrainingConfiguration : IEntityTypeConfiguration<Training>
{
    public void Configure(EntityTypeBuilder<Training> builder)
    {
        builder.Property(t => t.Description).HasMaxLength(300).IsRequired();
        builder.Property(t => t.TrainingType).HasConversion<int>();
        builder.Property(t => t.Status).HasConversion<int>();

        // Restrict: a coach who created trainings must not be deletable in a way that silently
        // removes the club's training history. Members are deactivated, never deleted anyway.
        builder.HasOne(t => t.Member)
            .WithMany()
            .HasForeignKey(t => t.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Club)
            .WithMany(c => c.Trainings)
            .HasForeignKey(t => t.ClubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.ClubId);
        builder.HasIndex(t => t.Date);
        builder.HasIndex(t => t.Status);
    }
}
