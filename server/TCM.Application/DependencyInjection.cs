using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCM.Application.Options;
using TCM.Application.Services;
using TCM.Application.Validation;

namespace TCM.Application;

/// <summary>Registers the application layer: business services and their bound settings.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<ClientSettings>(configuration.GetSection(ClientSettings.SectionName));
        services.Configure<StripeSettings>(configuration.GetSection(StripeSettings.SectionName));
        services.Configure<GmailSettings>(configuration.GetSection(GmailSettings.SectionName));
        services.Configure<PhotoSettings>(configuration.GetSection(PhotoSettings.SectionName));

        // Validators are discovered by assembly scan, so a new one is live as soon as it exists.
        services.AddValidatorsFromAssemblyContaining<MemberRegisterDtoValidator>(ServiceLifetime.Singleton);

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICommonService, CommonService>();
        services.AddScoped<IPhotoService, PhotoService>();
        services.AddScoped<IPaymentService, PaymentService>();

        return services;
    }
}
