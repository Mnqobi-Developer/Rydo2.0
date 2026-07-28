namespace Rydo.Application.Maps;

public sealed record GeoCoordinate(double Latitude, double Longitude);

public sealed record PlacePredictionResult(
    string PlaceId,
    string MainText,
    string SecondaryText,
    string Description);

public sealed record PlaceResult(
    string PlaceId,
    string Name,
    string Address,
    GeoCoordinate Location);

public sealed record RouteRequest(GeoCoordinate Origin, GeoCoordinate Destination);

public sealed record RouteResult(
    int DistanceMeters,
    int DurationSeconds,
    string EncodedPolyline);

public interface IMapService
{
    Task<IReadOnlyList<PlacePredictionResult>> AutocompleteAsync(
        string query,
        string sessionToken,
        GeoCoordinate? locationBias,
        CancellationToken cancellationToken);

    Task<PlaceResult?> GetPlaceAsync(
        string placeId,
        string sessionToken,
        CancellationToken cancellationToken);

    Task<PlaceResult?> ReverseGeocodeAsync(
        GeoCoordinate location,
        CancellationToken cancellationToken);

    Task<RouteResult?> ComputeRouteAsync(
        RouteRequest request,
        CancellationToken cancellationToken);
}

public sealed class MapProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
