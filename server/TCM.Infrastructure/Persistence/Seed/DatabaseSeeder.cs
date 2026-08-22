using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCM.Domain.Constants;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Persistence.Seed;

/// <summary>
/// Brings a fresh database up to a usable state: the two roles, the belt lookup, one club, and
/// one coach account. Every step checks before it writes, so running this repeatedly is safe.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>Belt lookup in grading order, lowest first.</summary>
    private static readonly (string Name, int Rank)[] Belts =
    [
        ("White", 1),
        ("Yellow Stripe", 2),
        ("Yellow", 3),
        ("Green Stripe", 4),
        ("Green", 5),
        ("Blue Stripe", 6),
        ("Blue", 7),
        ("Red Stripe", 8),
        ("Red", 9),
        ("Black Stripe", 10),
        ("Black Belt 1st Dan", 11),
        ("Black Belt 2nd Dan", 12),
        ("Black Belt 3rd Dan", 13)
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseSeeder));
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = sp.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager, logger);
        await SeedBeltsAsync(db, logger, ct);
        var club = await SeedClubAsync(db, configuration, logger, ct);
        await SeedCoachAsync(userManager, db, configuration, club, logger, ct);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role)) continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (result.Succeeded)
                logger.LogInformation("Seeded role {Role}.", role);
            else
                logger.LogError("Could not seed role {Role}: {Errors}", role, Describe(result));
        }
    }

    private static async Task SeedBeltsAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        var existing = await db.Belts.Select(b => b.BeltName).ToListAsync(ct);
        var missing = Belts
            .Where(b => !existing.Contains(b.Name))
            .Select(b => new Belt { BeltName = b.Name, Rank = b.Rank })
            .ToList();

        if (missing.Count == 0) return;

        db.Belts.AddRange(missing);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} belts.", missing.Count);
    }

    private static async Task<Club> SeedClubAsync(
        ApplicationDbContext db, IConfiguration configuration, ILogger logger, CancellationToken ct)
    {
        var existing = await db.Clubs.FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var club = new Club
        {
            Name = configuration["Seed:ClubName"] ?? "Taekwondo Club",
            Address = configuration["Seed:ClubAddress"]
        };

        db.Clubs.Add(club);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded club {ClubName}.", club.Name);
        return club;
    }

    private static async Task SeedCoachAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IConfiguration configuration,
        Club club,
        ILogger logger,
        CancellationToken ct)
    {
        var email = configuration["Seed:CoachEmail"];
        var password = configuration["Seed:CoachPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            // Not fatal: the app must start without seed credentials configured.
            logger.LogWarning(
                "Seed:CoachEmail / Seed:CoachPassword are not configured, so no coach account was created. " +
                "Set them with 'dotnet user-secrets set' to get a login. There is no public sign-up (SPEC section 6.1).");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null) return;

        var coach = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = configuration["Seed:CoachFirstName"] ?? "Head",
            LastName = configuration["Seed:CoachLastName"] ?? "Coach",
            IsCoach = true,
            IsActive = true,
            DateOfBirth = new DateOnly(1990, 1, 1),
            StartedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            ClubId = club.Id
        };

        var created = await userManager.CreateAsync(coach, password);
        if (!created.Succeeded)
        {
            logger.LogError("Could not seed the coach account: {Errors}", Describe(created));
            return;
        }

        var roled = await userManager.AddToRoleAsync(coach, Roles.Coach);
        if (!roled.Succeeded)
        {
            logger.LogError("Could not assign the Coach role: {Errors}", Describe(roled));
            return;
        }

        // Give the coach a starting belt so the profile screens have something to render.
        var blackBelt = await db.Belts.FirstOrDefaultAsync(b => b.BeltName == "Black Belt 1st Dan", ct);
        if (blackBelt is not null)
        {
            db.MemberBelts.Add(new MemberBelt
            {
                MemberId = coach.Id,
                BeltId = blackBelt.Id,
                DateReceived = coach.StartedOn,
                IsCurrentBelt = true
            });
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Seeded coach account {Email}.", email);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
