using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rydo.Application.Maps;

namespace Rydo.Api.Tests;

public sealed class MapsApiTests
{
    [Fact]
    public async Task MapEndpointsRequireAuthentication()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/maps/places/autocomplete?query=Sandton&sessionToken=session-123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PassengerCanSearchResolveGeocodeAndRoute()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(client, "+27820000601", "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var predictions = await client.GetFromJsonAsync<List<PlacePredictionResult>>(
            "/api/v1/maps/places/autocomplete?query=Sandton&sessionToken=session-123&latitude=-26.1&longitude=28.0");
        Assert.Single(predictions!);

        var place = await client.GetFromJsonAsync<PlaceResult>(
            "/api/v1/maps/places/place-1?sessionToken=session-123");
        Assert.Equal("Sandton City", place!.Name);

        var reverse = await client.GetFromJsonAsync<PlaceResult>(
            "/api/v1/maps/geocode/reverse?latitude=-26.1076&longitude=28.0567");
        Assert.Equal("Sandton City", reverse!.Name);

        var routeResponse = await client.PostAsJsonAsync("/api/v1/maps/routes", new
        {
            origin = new { latitude = -26.1076, longitude = 28.0567 },
            destination = new { latitude = -26.2041, longitude = 28.0473 },
        });
        routeResponse.EnsureSuccessStatusCode();
        var route = await routeResponse.Content.ReadFromJsonAsync<RouteResult>();
        Assert.Equal(14_200, route!.DistanceMeters);
    }

    [Fact]
    public async Task InvalidRouteCoordinatesAreRejected()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(client, "+27820000602", "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/maps/routes", new
        {
            origin = new { latitude = 100, longitude = 28 },
            destination = new { latitude = -26, longitude = 28 },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static AuthenticationApiFactory CreateFactory() => new(services =>
    {
        services.RemoveAll<IMapService>();
        services.AddSingleton<IMapService, FakeMapService>();
    });

    private sealed class FakeMapService : IMapService
    {
        private static readonly PlaceResult Place = new(
            "place-1", "Sandton City", "83 Rivonia Road, Sandton",
            new GeoCoordinate(-26.1076, 28.0567));

        public Task<IReadOnlyList<PlacePredictionResult>> AutocompleteAsync(
            string query, string sessionToken, GeoCoordinate? locationBias,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlacePredictionResult>>([
                new("place-1", "Sandton City", "Sandton, South Africa", "Sandton City, Sandton"),
            ]);

        public Task<PlaceResult?> GetPlaceAsync(
            string placeId, string sessionToken, CancellationToken cancellationToken) =>
            Task.FromResult<PlaceResult?>(Place);

        public Task<PlaceResult?> ReverseGeocodeAsync(
            GeoCoordinate location, CancellationToken cancellationToken) =>
            Task.FromResult<PlaceResult?>(Place);

        public Task<RouteResult?> ComputeRouteAsync(
            RouteRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<RouteResult?>(new(14_200, 1_320, "_p~iF~ps|U_ulLnnqC_mqNvxq`@"));
    }
}
