// Microsoft.OpenApi v2 (pulled in by Swashbuckle 10) moved these types out of the old
// Microsoft.OpenApi.Models namespace and up into Microsoft.OpenApi.
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using TCM.Api.Middleware;
using TCM.Application;
using TCM.Application.Options;
using TCM.Infrastructure;
using TCM.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging -------------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---- MVC -----------------------------------------------------------------------------------
builder.Services.AddControllers();

// ---- Application + Infrastructure -----------------------------------------------------------
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Authentication ---------------------------------------------------------------------------
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < JwtSettings.MinimumKeyLengthBytes)
{
    throw new InvalidOperationException(
        $"Jwt:Key must be configured and at least {JwtSettings.MinimumKeyLengthBytes} bytes long. " +
        "Set Jwt:Key, Jwt:Issuer and Jwt:Audience with 'dotnet user-secrets set' or environment variables.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            // Default is five minutes, which would keep expired tokens working well past expiry.
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

// ---- CORS ----------------------------------------------------------------------------------
// Origins come from configuration so no deployment environment is baked into the code (SPEC section 9).
const string ClientCorsPolicy = "ClientCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // No origins configured: allow nothing rather than silently allowing everything.
            policy.WithOrigins().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    }));

// ---- OpenAPI -------------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TCM API",
        Version = "v1",
        Description = "Taekwondo Club Management API"
    });

    // Lets the Swagger UI send a bearer token, which is how auth gets exercised by hand.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by /api/account/login. No \"Bearer \" prefix needed."
    });

    // Swashbuckle 10 takes a factory here rather than the requirement itself.
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

var app = builder.Build();

// ---- Integration status ----------------------------------------------------------------------
// Stripe is deferred by decision (2026-08-22). Say so loudly at every start, so a deployment
// cannot quietly ship the local fake believing it is taking real payments.
var stripeSettings = builder.Configuration.GetSection(StripeSettings.SectionName).Get<StripeSettings>() ?? new StripeSettings();
var gmailSettings = builder.Configuration.GetSection(GmailSettings.SectionName).Get<GmailSettings>() ?? new GmailSettings();

if (!stripeSettings.Enabled)
{
    app.Logger.LogWarning(
        "Stripe:Enabled is FALSE. Membership payments run through a LOCAL FAKE and no money moves. " +
        "Set Stripe:Enabled=true with real keys before taking real payments.");

    if (app.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "Refusing to start in Production with Stripe:Enabled=false — that would accept " +
            "membership payments that never charge anyone.");
    }
}

if (!gmailSettings.IsConfigured)
{
    app.Logger.LogWarning(
        "Gmail SMTP is not configured. Emails are written to the log instead of being sent.");
}

// ---- Seed ----------------------------------------------------------------------------------
// Idempotent, so it is safe on every start. Development only: production databases are brought
// up with a migration script instead (see plan.md phase 12).
if (app.Environment.IsDevelopment())
{
    await DatabaseSeeder.SeedAsync(app.Services);
}

// ---- Pipeline ------------------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "TCM API v1"));
}

app.UseHttpsRedirection();
app.UseCors(ClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the API in tests.</summary>
public partial class Program;
