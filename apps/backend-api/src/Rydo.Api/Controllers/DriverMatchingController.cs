using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Matching;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "driver")]
[EnableRateLimiting("api")]
[Route("api/v1/drivers/me")]
public sealed class DriverMatchingController(IDriverMatchingService matching) : ControllerBase
{
    [HttpGet("availability")]
    [ProducesResponseType<DriverAvailabilityResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverAvailabilityResult>> GetAvailability(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var availability = await matching.GetAvailabilityAsync(userId, cancellationToken);
        return availability is null ? NotFound() : Ok(availability);
    }

    [HttpPost("availability/online")]
    [ProducesResponseType<DriverAvailabilityResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<DriverAvailabilityResult>> GoOnline(
        DriverLocationRequest request,
        CancellationToken cancellationToken)
    {
        return RunAvailabilityCommandAsync(
            userId => matching.GoOnlineAsync(
                userId,
                request.Latitude,
                request.Longitude,
                cancellationToken));
    }

    [HttpPost("availability/offline")]
    [ProducesResponseType<DriverAvailabilityResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverAvailabilityResult>> GoOffline(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var availability = await matching.GoOfflineAsync(userId, cancellationToken);
            return availability is null ? NotFound() : Ok(availability);
        }
        catch (DriverAvailabilityConflictException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    [HttpPost("location")]
    [EnableRateLimiting("driver-location")]
    [ProducesResponseType<DriverAvailabilityResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<DriverAvailabilityResult>> UpdateLocation(
        DriverLocationRequest request,
        CancellationToken cancellationToken)
    {
        return RunAvailabilityCommandAsync(
            userId => matching.UpdateLocationAsync(
                userId,
                request.Latitude,
                request.Longitude,
                cancellationToken));
    }

    [HttpGet("trip-offers")]
    [ProducesResponseType<IReadOnlyList<TripOfferResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TripOfferResult>>> ListOffers(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await matching.ListOffersAsync(userId, cancellationToken));
    }

    [HttpPost("trip-offers/{tripId:guid}/decline")]
    [ProducesResponseType<TripOfferResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripOfferResult>> DeclineOffer(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await matching.DeclineOfferAsync(
                tripId,
                userId,
                cancellationToken));
        }
        catch (TripOfferNotFoundException)
        {
            return NotFound();
        }
        catch (TripMatchingStateException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    private async Task<ActionResult<DriverAvailabilityResult>> RunAvailabilityCommandAsync(
        Func<Guid, Task<DriverAvailabilityResult>> command)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await command(userId));
        }
        catch (DriverNotEligibleException exception)
        {
            return ConflictProblem(exception.Message);
        }
        catch (DriverAvailabilityNotFoundException)
        {
            return NotFound();
        }
        catch (DriverAvailabilityConflictException exception)
        {
            return ConflictProblem(exception.Message);
        }
        catch (TripMatchingStateException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    private ConflictObjectResult ConflictProblem(string detail)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Driver matching conflict",
            Detail = detail,
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue("sub"), out userId);
    }
}

public sealed record DriverLocationRequest(
    [Range(-90, 90)]
    double Latitude,
    [Range(-180, 180)]
    double Longitude);
