using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCM.Application.Options;
using TCM.Application.Services;

namespace TCM.Application;

/// <summary>Registers the application layer: business services and their bound settings.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<ClientSettings>(configuration.GetSection(ClientSettings.SectionName));

        services.AddScoped<IAccountService, AccountService>();

        return services;
    }
}
