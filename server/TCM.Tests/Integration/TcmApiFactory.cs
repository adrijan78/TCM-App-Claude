using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TCM.Domain.Constants;
using TCM.Domain.Entities;
using TCM.Infrastructure.Persistence;

namespace TCM.Tests.Integration;

/// <summary>
/// Boots the real API — real pipeline, real JWT validation, real authorization attributes —
/// against a throwaway SQLite database. Tests obtain tokens by calling the login endpoint, so
/// what they exercise is the same path a browser takes rather than a stubbed identity.
/// </summary>
public class TcmApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string CoachEmail = "coach@test.local";
    public const string MemberEmail = "member@test.local";
    public const string OtherMemberEmail = "other@test.local";
    public const string Password = "TestPassw0rd!";

    /// <summary>
    /// Held open for the lifetime of the factory: a SQLite in-memory database exists only as
    /// long as at least one connection to it is open.
    /// </summary>
    private DbConnection? _connection;

    public string CoachId { get; private set; } = string.Empty;
    public string MemberId { get; private set; } = string.Empty;
    public string OtherMemberId { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development, so Program.cs skips its seeder and this class controls the data.
        builder.UseEnvironment(Environments.Staging);

        // UseSetting, not ConfigureAppConfiguration: under minimal hosting, Program.cs reads
        // builder.Configuration while the host is still being built, which is before
        // ConfigureAppConfiguration callbacks run. UseSetting values are in place early enough.
        var settings = new Dictionary<string, string>
        {
            // Non-empty so AddInfrastructure's guard passes; the provider is replaced below.
            ["ConnectionStrings:Default"] = "Server=unused;Database=unused;",
            ["Jwt:Key"] = "test-signing-key-that-is-comfortably-long-enough-for-hmac-sha256",
            ["Jwt:Issuer"] = "TCM.Api.Tests",
            ["Jwt:Audience"] = "TCM.Client.Tests",
            ["Jwt:ExpiryMinutes"] = "60",
            ["Client:BaseUrl"] = "http://localhost:4200",
            ["Cors:AllowedOrigins:0"] = "http://localhost:4200",

            // Stripe stays disabled, so FakeCheckoutService carries the payment flow. The URLs
            // still have to be real ones — the fake builds its redirect from SuccessUrl.
            ["Stripe:Enabled"] = "false",
            ["Stripe:SuccessUrl"] = "http://localhost:4200/successful-payment",
            ["Stripe:CancelUrl"] = "http://localhost:4200/failed-payment",
            ["Stripe:MembershipDays"] = "30",

            ["Photos:MaxSizeBytes"] = "2097152"
        };

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            RemoveAll(services, typeof(DbContextOptions<ApplicationDbContext>));
            RemoveAll(services, typeof(DbContextOptions));
            RemoveAll(services, typeof(ApplicationDbContext));

            // EF Core 9 moved the provider selection into IDbContextOptionsConfiguration<T>
            // descriptors. Dropping only DbContextOptions leaves UseSqlServer still applied, and
            // EF then refuses to start with two providers registered.
            RemoveAllByName(services, "IDbContextOptionsConfiguration");

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    private static void RemoveAll(IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == serviceType).ToList())
        {
            services.Remove(descriptor);
        }
    }

    private static void RemoveAllByName(IServiceCollection services, string serviceTypeNamePrefix)
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType.Name.StartsWith(serviceTypeNamePrefix, StringComparison.Ordinal))
                     .ToList())
        {
            services.Remove(descriptor);
        }
    }

    // Implemented explicitly: xUnit v2's IAsyncLifetime declares Task DisposeAsync(), while
    // WebApplicationFactory already has ValueTask DisposeAsync() from IAsyncDisposable. The two
    // cannot both be satisfied implicitly, so the interface members forward to the real work.
    Task IAsyncLifetime.InitializeAsync() => SeedAsync();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    private async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }

        var club = new Club { Name = "Test Club" };
        db.Clubs.Add(club);
        await db.SaveChangesAsync();

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        CoachId = await CreateUserAsync(userManager, CoachEmail, "Test", "Coach", Roles.Coach, club.Id);
        MemberId = await CreateUserAsync(userManager, MemberEmail, "Test", "Member", Roles.Member, club.Id);
        OtherMemberId = await CreateUserAsync(userManager, OtherMemberEmail, "Other", "Member", Roles.Member, club.Id);
    }

    private static async Task<string> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email, string firstName, string lastName, string role, int clubId)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            IsCoach = role == Roles.Coach,
            IsActive = true,
            DateOfBirth = new DateOnly(2000, 1, 1),
            StartedOn = new DateOnly(2024, 1, 1),
            ClubId = clubId
        };

        var created = await userManager.CreateAsync(user, Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Test fixture could not create {email}: " +
                string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, role);
        return user.Id;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
