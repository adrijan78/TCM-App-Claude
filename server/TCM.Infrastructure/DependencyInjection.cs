using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCM.Domain.Entities;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure;

/// <summary>
/// Registers everything the infrastructure layer owns. Keeping the registrations here rather
/// than in <c>Program.cs</c> stops the composition root growing without limit.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it with 'dotnet user-secrets set' " +
                "or an environment variable — it is never committed to source (SPEC section 9).");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        // Password-reset tokens are produced by DataProtectorTokenProvider, which needs Data
        // Protection registered. AddIdentityCore does not bring it in on its own.
        // Note for deployment: the default key ring is per-machine, so a multi-instance
        // deployment must persist keys to shared storage or reset links will break across nodes.
        services.AddDataProtection();

        // Identity core only: this app authenticates with JWTs, so it needs UserManager,
        // RoleManager and the password hasher, but none of the cookie/UI machinery.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
