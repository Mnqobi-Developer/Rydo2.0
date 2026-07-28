using System.Net;
using System.Text;
using Rydo.Application.Maps;
using Rydo.Infrastructure.Maps;

namespace Rydo.Api.Tests;

public sealed class GoogleMapServiceTests
{
    [Fact]
    public async Task ComputeRouteRoundsFractionalGoogleDurationUp()
    {
        using var response = JsonResponse(
            """{"routes":[{"duration":"123.5s","distanceMeters":4200,"polyline":{"encodedPolyline":"route"}}]}""");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(response));
        var service = CreateService(httpClient);

        var route = await service.ComputeRouteAsync(
            new RouteRequest(
                new GeoCoordinate(-26.1076, 28.0567),
                new GeoCoordinate(-26.2041, 28.0473)),
            CancellationToken.None);

        Assert.NotNull(route);
        Assert.Equal(124, route.DurationSeconds);
    }

    [Fact]
    public async Task ReverseGeocodeReportsProviderConfigurationFailures()
    {
        using var response = JsonResponse(
            """{"status":"REQUEST_DENIED","error_message":"Billing is disabled."}""");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(response));
        var service = CreateService(httpClient);

        var exception = await Assert.ThrowsAsync<MapProviderUnavailableException>(() =>
            service.ReverseGeocodeAsync(
                new GeoCoordinate(-26.1076, 28.0567),
                CancellationToken.None));

        Assert.Equal(
            "Google Maps rejected the reverse-geocoding request.",
            exception.Message);
    }

    private static GoogleMapService CreateService(HttpClient httpClient) => new(
        httpClient,
        Microsoft.Extensions.Options.Options.Create(
            new GoogleMapsOptions { ServerApiKey = "test-key" }));

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHttpMessageHandler(HttpResponseMessage response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
