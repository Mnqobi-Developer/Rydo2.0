using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rydo.Infrastructure.Persistence;
using Rydo.Application.Maps;

namespace Rydo.Api.Tests;

public sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"rydo-auth-tests-{Guid.NewGuid():N}";
    private readonly Action<IServiceCollection>? _configureTestServices;

    public AuthenticationApiFactory()
    {
    }

    internal AuthenticationApiFactory(Action<IServiceCollection> configureTestServices)
    {
        _configureTestServices = configureTestServices;
    }

    public AdjustableTimeProvider Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAccess:Enabled"] = "true",
                ["AdminAccess:BootstrapEmail"] = "admin@rydo.test",
                ["AdminAccess:BootstrapPhoneNumber"] = "+27829999999",
                ["AdminAccess:BootstrapPassword"] = "test-only-admin-password",
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RydoDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RydoDbContext>>();
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<IMapService>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IMapService, TestMapService>();
            services.AddDbContext<RydoDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            _configureTestServices?.Invoke(services);
        });
    }
}

internal sealed class TestMapService : IMapService
{
    public Task<IReadOnlyList<PlacePredictionResult>> AutocompleteAsync(
        string query, string sessionToken, GeoCoordinate? locationBias,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlacePredictionResult>>([]);

    public Task<PlaceResult?> GetPlaceAsync(
        string placeId, string sessionToken, CancellationToken cancellationToken) =>
        Task.FromResult<PlaceResult?>(null);

    public Task<PlaceResult?> ReverseGeocodeAsync(
        GeoCoordinate location, CancellationToken cancellationToken) =>
        Task.FromResult<PlaceResult?>(null);

    public Task<RouteResult?> ComputeRouteAsync(
        RouteRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<RouteResult?>(new(2_000, 600, "test-route"));
}

public sealed class AdjustableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }
}
