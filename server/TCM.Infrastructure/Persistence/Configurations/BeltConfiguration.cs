using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Configurations;

public class BeltConfiguration : IEntityTypeConfiguration<Belt>
{
    public void Configure(EntityTypeBuilder<Belt> builder)
    {
        builder.Property(b => b.BeltName).HasMaxLength(50).IsRequired();
        builder.HasIndex(b => b.BeltName).IsUnique();
    }
}
