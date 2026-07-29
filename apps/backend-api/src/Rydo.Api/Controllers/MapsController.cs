using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Maps;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("maps")]
[Route("api/v1/maps")]
public sealed class MapsController(IMapService maps) : ControllerBase
{
    [HttpGet("places/autocomplete")]
    public Task<ActionResult<IReadOnlyList<PlacePredictionResult>>> Autocomplete(
        [FromQuery, MinLength(3), MaxLength(160)] string query,
        [FromQuery, MinLength(8), MaxLength(36)] string sessionToken,
        [FromQuery, Range(-90, 90)] double? latitude,
        [FromQuery, Range(-180, 180)] double? longitude,
        CancellationToken cancellationToken) => RunAsync<IReadOnlyList<PlacePredictionResult>>(async () =>
        Ok(await maps.AutocompleteAsync(
            query.Trim(),
            sessionToken,
            latitude.HasValue && longitude.HasValue
                ? new GeoCoordinate(latitude.Value, longitude.Value)
                : null,
            cancellationToken)));

    [HttpGet("places/{placeId}")]
    public Task<ActionResult<PlaceResult>> GetPlace(
        [FromRoute, MinLength(1), MaxLength(512)] string placeId,
        [FromQuery, MinLength(8), MaxLength(36)] string sessionToken,
        CancellationToken cancellationToken) => RunAsync<PlaceResult>(async () =>
    {
        var place = await maps.GetPlaceAsync(placeId, sessionToken, cancellationToken);
        return place is null ? NotFound() : Ok(place);
    });

    [HttpGet("geocode/reverse")]
    public Task<ActionResult<PlaceResult>> ReverseGeocode(
        [FromQuery, Range(-90, 90)] double latitude,
        [FromQuery, Range(-180, 180)] double longitude,
        CancellationToken cancellationToken) => RunAsync<PlaceResult>(async () =>
    {
        var place = await maps.ReverseGeocodeAsync(
            new GeoCoordinate(latitude, longitude), cancellationToken);
        return place is null ? NotFound() : Ok(place);
    });

    [HttpPost("routes")]
    public Task<ActionResult<RouteResult>> ComputeRoute(
        RouteRequest request,
        CancellationToken cancellationToken) => RunAsync<RouteResult>(async () =>
    {
        if (!IsValid(request.Origin) || !IsValid(request.Destination))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["location"] = ["Origin and destination must contain valid coordinates."],
            }));
        }

        var route = await maps.ComputeRouteAsync(request, cancellationToken);
        return route is null ? NotFound() : Ok(route);
    });

    private async Task<ActionResult<T>> RunAsync<T>(Func<Task<ActionResult<T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (MapProviderUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Map service unavailable",
                detail: exception.Message);
        }
    }

    private static bool IsValid(GeoCoordinate? value) =>
        value is not null
        && value.Latitude is >= -90 and <= 90
        && value.Longitude is >= -180 and <= 180;
}
