// Microsoft.OpenApi v2 (pulled in by Swashbuckle 10) moved these types out of the old
// Microsoft.OpenApi.Models namespace and up into Microsoft.OpenApi.
using Microsoft.OpenApi;
using Serilog;
using TCM.Api.Middleware;
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

// ---- Infrastructure (database, repositories, external services) -----------------------------
builder.Services.AddInfrastructure(builder.Configuration);

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

app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the API in tests.</summary>
public partial class Program;
