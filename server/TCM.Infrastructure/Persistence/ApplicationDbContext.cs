using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence;

/// <summary>
/// The TCM database (SPEC section 4). Identity supplies AspNetUsers/Roles/UserRoles; the
/// domain tables are configured by the <c>IEntityTypeConfiguration</c> classes in
/// <c>Persistence/Configurations</c>.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Belt> Belts => Set<Belt>();
    public DbSet<MemberBelt> MemberBelts => Set<MemberBelt>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Photo> Photos => Set<Photo>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
