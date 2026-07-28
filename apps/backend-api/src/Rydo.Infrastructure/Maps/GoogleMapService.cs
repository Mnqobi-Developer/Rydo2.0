using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rydo.Application.Maps;

namespace Rydo.Infrastructure.Maps;

public sealed class GoogleMapService(
    HttpClient httpClient,
    IOptions<GoogleMapsOptions> options) : IMapService
{
    private static readonly string[] SouthAfricaRegion = ["za"];
    private readonly string _apiKey = options.Value.ServerApiKey;

    public async Task<IReadOnlyList<PlacePredictionResult>> AutocompleteAsync(
        string query,
        string sessionToken,
        GeoCoordinate? locationBias,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post,
            "https://places.googleapis.com/v1/places:autocomplete");
        request.Headers.Add("X-Goog-FieldMask",
            "suggestions.placePrediction.placeId,suggestions.placePrediction.text,suggestions.placePrediction.structuredFormat");
        request.Content = JsonContent.Create(new
        {
            input = query,
            sessionToken,
            includedRegionCodes = SouthAfricaRegion,
            languageCode = "en",
            locationBias = locationBias is null ? null : new
            {
                circle = new
                {
                    center = new { latitude = locationBias.Latitude, longitude = locationBias.Longitude },
                    radius = 50_000,
                },
            },
        });

        using var document = await SendAsync(request, cancellationToken);
        if (!document.RootElement.TryGetProperty("suggestions", out var suggestions))
        {
            return [];
        }

        return suggestions.EnumerateArray()
            .Where(item => item.TryGetProperty("placePrediction", out _))
            .Select(item => item.GetProperty("placePrediction"))
            .Select(prediction =>
            {
                var structured = prediction.GetProperty("structuredFormat");
                var description = prediction.GetProperty("text").GetProperty("text").GetString() ?? string.Empty;
                return new PlacePredictionResult(
                    prediction.GetProperty("placeId").GetString() ?? string.Empty,
                    structured.GetProperty("mainText").GetProperty("text").GetString() ?? description,
                    structured.TryGetProperty("secondaryText", out var secondary)
                        ? secondary.GetProperty("text").GetString() ?? string.Empty
                        : string.Empty,
                    description);
            })
            .Where(result => result.PlaceId.Length > 0)
            .ToArray();
    }

    public async Task<PlaceResult?> GetPlaceAsync(
        string placeId,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}?sessionToken={Uri.EscapeDataString(sessionToken)}");
        request.Headers.Add("X-Goog-FieldMask", "id,displayName,formattedAddress,location");
        using var document = await SendAsync(request, cancellationToken);
        return ParsePlace(document.RootElement);
    }

    public async Task<PlaceResult?> ReverseGeocodeAsync(
        GeoCoordinate location,
        CancellationToken cancellationToken)
    {
        var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);
        using var request = CreateRequest(HttpMethod.Get,
            $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&region=za&key={Uri.EscapeDataString(RequireApiKey())}",
            includeKeyHeader: false);
        using var document = await SendAsync(request, cancellationToken);
        var status = document.RootElement.TryGetProperty("status", out var statusValue)
            ? statusValue.GetString()
            : null;
        if (string.Equals(status, "ZERO_RESULTS", StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.Equals(status, "OK", StringComparison.Ordinal))
        {
            throw new MapProviderUnavailableException(
                "Google Maps rejected the reverse-geocoding request.");
        }

        if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            return null;
        }

        var result = results[0];
        return new PlaceResult(
            result.GetProperty("place_id").GetString() ?? string.Empty,
            result.GetProperty("formatted_address").GetString() ?? "Selected location",
            result.GetProperty("formatted_address").GetString() ?? "Selected location",
            location);
    }

    public async Task<RouteResult?> ComputeRouteAsync(
        RouteRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post,
            "https://routes.googleapis.com/directions/v2:computeRoutes");
        httpRequest.Headers.Add("X-Goog-FieldMask",
            "routes.duration,routes.distanceMeters,routes.polyline.encodedPolyline");
        httpRequest.Content = JsonContent.Create(new
        {
            origin = Waypoint(request.Origin),
            destination = Waypoint(request.Destination),
            travelMode = "DRIVE",
            routingPreference = "TRAFFIC_AWARE",
            units = "METRIC",
            polylineQuality = "OVERVIEW",
        });
        using var document = await SendAsync(httpRequest, cancellationToken);
        if (!document.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
        {
            return null;
        }

        var result = routes[0];
        var duration = result.GetProperty("duration").GetString() ?? "0s";
        return new RouteResult(
            result.GetProperty("distanceMeters").GetInt32(),
            ParseDurationSeconds(duration),
            result.GetProperty("polyline").GetProperty("encodedPolyline").GetString() ?? string.Empty);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, bool includeKeyHeader = true)
    {
        var request = new HttpRequestMessage(method, url);
        if (includeKeyHeader)
        {
            request.Headers.Add("X-Goog-Api-Key", RequireApiKey());
        }

        return request;
    }

    private async Task<JsonDocument> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new MapProviderUnavailableException(
                    $"Google Maps returned HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (MapProviderUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new MapProviderUnavailableException("Google Maps is temporarily unavailable.", exception);
        }
    }

    private string RequireApiKey()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new MapProviderUnavailableException(
                "Google Maps server credentials are not configured.");
        }

        return _apiKey;
    }

    private static object Waypoint(GeoCoordinate location) => new
    {
        location = new
        {
            latLng = new { latitude = location.Latitude, longitude = location.Longitude },
        },
    };

    private static int ParseDurationSeconds(string duration) =>
        double.TryParse(
            duration.TrimEnd('s'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var seconds)
            ? (int)Math.Ceiling(seconds)
            : 0;

    private static PlaceResult ParsePlace(JsonElement place)
    {
        var location = place.GetProperty("location");
        return new PlaceResult(
            place.GetProperty("id").GetString() ?? string.Empty,
            place.GetProperty("displayName").GetProperty("text").GetString() ?? "Selected location",
            place.GetProperty("formattedAddress").GetString() ?? "Selected location",
            new GeoCoordinate(
                location.GetProperty("latitude").GetDouble(),
                location.GetProperty("longitude").GetDouble()));
    }
}
