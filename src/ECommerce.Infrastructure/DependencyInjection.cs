using ECommerce.Application.Common;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Infrastructure.AI;
using ECommerce.Infrastructure.Currency;
using ECommerce.Infrastructure.Auth;
using ECommerce.Infrastructure.Email;
using ECommerce.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<SyncSettings>(configuration.GetSection(SyncSettings.SectionName));

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Email is stateless (new SmtpClient per call) → singleton is safe and
        // lets fire-and-forget sends outlive the request scope.
        services.AddSingleton<IEmailService, SmtpEmailService>();

        // Gemini client uses HttpClientFactory so connections are pooled and
        // we get sensible defaults (DNS rotation, automatic decompression).
        services.AddHttpClient<IGeminiClient, GeminiClient>();

        // Currency rates (CBAR + fallback) — pooled connections, cached 6h.
        services.AddHttpClient<ICurrencyRateProvider, CurrencyRateProvider>();

        return services;
    }
}
