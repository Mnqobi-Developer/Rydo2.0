using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rydo.Application.Authentication;
using Rydo.Infrastructure.Authentication;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var connectionString = configuration.GetConnectionString("RydoDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:RydoDatabase must be configured.");
        }

        services.AddDbContext<RydoDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CryptoTokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        if (isDevelopment)
        {
            services.AddScoped<IOtpDeliveryService, DevelopmentOtpDeliveryService>();
        }
        else
        {
            services.AddScoped<IOtpDeliveryService, UnavailableOtpDeliveryService>();
        }

        return services;
    }
}
