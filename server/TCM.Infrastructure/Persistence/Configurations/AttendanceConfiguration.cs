using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.Status).HasConversion<int>();

        builder.HasOne(a => a.Training)
            .WithMany(t => t.Attendances)
            .HasForeignKey(a => a.TrainingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on this side: Attendances can be reached from AspNetUsers both directly and
        // via Trainings, and SQL Server rejects two cascade paths into the same table.
        builder.HasOne(a => a.Member)
            .WithMany(u => u.Attendances)
            .HasForeignKey(a => a.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.TrainingId);
        builder.HasIndex(a => a.MemberId);

        // One invitation per member per training.
        builder.HasIndex(a => new { a.TrainingId, a.MemberId }).IsUnique();
    }
}
