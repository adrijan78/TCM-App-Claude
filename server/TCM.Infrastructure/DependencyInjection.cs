using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCM.Application.Abstractions;
using TCM.Application.Options;
using TCM.Domain.Entities;
using TCM.Infrastructure.Identity;
using TCM.Infrastructure.Integrations;
using TCM.Infrastructure.Persistence;
using TCM.Infrastructure.Repositories;

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

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICommonRepository, CommonRepository>();
        services.AddScoped<IPhotoRepository, PhotoRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ITrainingRepository, TrainingRepository>();

        // Members slice (SPEC sections 6.3 and 6.4).
        services.AddScoped<IMemberRepository, MemberRepository>();

        services.AddScoped<ITokenService, TokenService>();

        AddEmail(services, configuration);
        AddPayments(services, configuration);

        return services;
    }

    /// <summary>
    /// Real SMTP when Gmail is configured, a logging stand-in otherwise, so the app runs end to
    /// end on a developer machine with no mail credentials.
    /// </summary>
    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        var gmail = configuration.GetSection(GmailSettings.SectionName).Get<GmailSettings>() ?? new GmailSettings();

        if (gmail.IsConfigured)
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, LoggingEmailService>();
        }
    }

    /// <summary>
    /// Stripe is deferred by decision (2026-08-22). With <c>Stripe:Enabled</c> false the local
    /// fake keeps the whole membership-payment flow working; with it true the real Stripe
    /// integration takes over and nothing else changes.
    /// </summary>
    private static void AddPayments(IServiceCollection services, IConfiguration configuration)
    {
        var stripe = configuration.GetSection(StripeSettings.SectionName).Get<StripeSettings>() ?? new StripeSettings();

        if (stripe.Enabled)
        {
            services.AddScoped<ICheckoutService, StripeCheckoutService>();
            services.AddScoped<IStripeCustomerService, StripeCustomerService>();
        }
        else
        {
            services.AddScoped<ICheckoutService, FakeCheckoutService>();
            services.AddScoped<IStripeCustomerService, NoOpStripeCustomerService>();
        }
    }
}
