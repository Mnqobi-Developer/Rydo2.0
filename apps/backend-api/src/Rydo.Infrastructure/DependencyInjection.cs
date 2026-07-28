using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rydo.Application.Authentication;
using Rydo.Application.Admin;
using Rydo.Application.Drivers;
using Rydo.Application.Disputes;
using Rydo.Application.Matching;
using Rydo.Application.Maps;
using Rydo.Application.Passengers;
using Rydo.Application.Payments;
using Rydo.Application.Ratings;
using Rydo.Application.Trips;
using Rydo.Infrastructure.Authentication;
using Rydo.Infrastructure.Admin;
using Rydo.Infrastructure.Drivers;
using Rydo.Infrastructure.Disputes;
using Rydo.Infrastructure.Matching;
using Rydo.Infrastructure.Maps;
using Rydo.Infrastructure.Passengers;
using Rydo.Infrastructure.Payments;
using Rydo.Infrastructure.Persistence;
using Rydo.Infrastructure.Ratings;
using Rydo.Infrastructure.Trips;

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
        services.AddOptions<AdminAccessOptions>()
            .Bind(configuration.GetSection(AdminAccessOptions.SectionName))
            .Validate(options => options.IsValid(),
                "Enabled AdminAccess requires a valid bootstrap email, international phone number, and password of at least 16 characters.")
            .ValidateOnStart();
        services.AddScoped<AdminBootstrapService>();
        services.AddScoped<CryptoTokenService>();
        services.AddScoped<IAdminAuthenticationService, AdminAuthenticationService>();
        services.AddScoped<IAdminOperationsService, AdminOperationsService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IDriverDocumentService, DriverDocumentService>();
        services.AddScoped<IDriverProfileService, DriverProfileService>();
        services.AddScoped<IDriverVehicleService, DriverVehicleService>();
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IDriverMatchingService, DriverMatchingService>();
        services.AddScoped<IPassengerProfileService, PassengerProfileService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<ITripService, TripService>();
        services.AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName));
        services.AddSingleton(new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        })
        {
            Timeout = TimeSpan.FromSeconds(10),
        });
        services.AddSingleton<IMapService, GoogleMapService>();
        services.AddOptions<PayFastOptions>()
            .Bind(configuration.GetSection(PayFastOptions.SectionName));
        services.AddSingleton<PayFastHttpClient>();
        services.AddScoped<IPayFastGateway, PayFastGateway>();

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
