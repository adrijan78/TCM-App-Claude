using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.Property(p => p.FileName).HasMaxLength(260).IsRequired();
        builder.Property(p => p.ContentType).HasMaxLength(100).IsRequired();

        // No length cap at the column level, so the provider picks its own unbounded blob type —
        // varbinary(max) on SQL Server, BLOB on SQLite for the tests. Naming the SQL Server type
        // explicitly here would be valid on SQL Server and a syntax error everywhere else.
        // The real limit is Photos:MaxSizeBytes, enforced before the upload is buffered.
        builder.Property(p => p.Content).IsRequired();

        builder.HasIndex(p => p.PublicId).IsUnique();

        // The other half of the AspNetUsers <-> Photos cycle. Restrict on both sides.
        builder.HasOne(p => p.Member)
            .WithMany()
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.MemberId);
    }
}
