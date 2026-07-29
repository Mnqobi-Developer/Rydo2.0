using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Matching;
using Rydo.Application.Trips;
using Rydo.Domain.Identity;
using Rydo.Domain.Pricing;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "passenger,driver")]
[EnableRateLimiting("api")]
[Route("api/v1/trips")]
public sealed class TripsController(
    ITripService trips,
    IDriverMatchingService matching) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "passenger")]
    [EnableRateLimiting("trip-request")]
    [ProducesResponseType<TripResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripResult>> RequestTrip(
        RequestTripRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out _))
        {
            return Unauthorized();
        }

        try
        {
            var trip = await trips.RequestAsync(
                userId,
                request.PickupAddress,
                request.PickupLatitude,
                request.PickupLongitude,
                request.DestinationAddress,
                request.DestinationLatitude,
                request.DestinationLongitude,
                request.FareQuoteId,
                request.RideCategory,
                cancellationToken);

            return CreatedAtAction(nameof(GetTrip), new { tripId = trip.Id }, trip);
        }
        catch (PassengerProfileRequiredException exception)
        {
            return ConflictProblem("Passenger profile required", exception.Message);
        }
        catch (ActiveTripConflictException exception)
        {
            return ConflictProblem("Active trip conflict", exception.Message);
        }
        catch (FareQuoteConflictException exception)
        {
            return ConflictProblem("Fare quote conflict", exception.Message);
        }
        catch (TripValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid trip request",
                Detail = exception.Message,
            });
        }
        catch (TripAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyList<TripResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TripResult>>> ListTrips(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        return Ok(await trips.ListAsync(userId, role, cancellationToken));
    }

    [HttpGet("{tripId:guid}")]
    [ProducesResponseType<TripResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripResult>> GetTrip(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        var trip = await trips.GetAsync(tripId, userId, role, cancellationToken);
        return trip is null ? NotFound() : Ok(trip);
    }

    [HttpPost("{tripId:guid}/accept")]
    [Authorize(Roles = "driver")]
    public Task<ActionResult<TripResult>> Accept(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return RunDriverTransitionAsync(
            tripId,
            matching.AcceptOfferAsync,
            cancellationToken);
    }

    [HttpPost("{tripId:guid}/matching")]
    [Authorize(Roles = "passenger")]
    [EnableRateLimiting("trip-matching")]
    [ProducesResponseType<TripMatchingResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripMatchingResult>> Match(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out _))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await matching.MatchAsync(tripId, userId, cancellationToken));
        }
        catch (TripNotFoundException)
        {
            return NotFound();
        }
        catch (TripMatchingAccessException)
        {
            return Forbid();
        }
        catch (TripMatchingStateException exception)
        {
            return ConflictProblem("Trip matching conflict", exception.Message);
        }
    }

    [HttpPost("{tripId:guid}/arrive")]
    [Authorize(Roles = "driver")]
    public Task<ActionResult<TripResult>> Arrive(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return RunDriverTransitionAsync(
            tripId,
            trips.MarkDriverArrivedAsync,
            cancellationToken);
    }

    [HttpPost("{tripId:guid}/start")]
    [Authorize(Roles = "driver")]
    public Task<ActionResult<TripResult>> Start(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return RunDriverTransitionAsync(tripId, trips.StartAsync, cancellationToken);
    }

    [HttpPost("{tripId:guid}/complete")]
    [Authorize(Roles = "driver")]
    public Task<ActionResult<TripResult>> Complete(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return RunDriverTransitionAsync(tripId, trips.CompleteAsync, cancellationToken);
    }

    [HttpPost("{tripId:guid}/cancel")]
    [ProducesResponseType<TripResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripResult>> Cancel(
        Guid tripId,
        CancelTripRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        return await RunTransitionAsync(
            () => trips.CancelAsync(
                tripId,
                userId,
                role,
                request.Reason,
                cancellationToken));
    }

    private async Task<ActionResult<TripResult>> RunDriverTransitionAsync(
        Guid tripId,
        Func<Guid, Guid, CancellationToken, Task<TripResult>> transition,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out _))
        {
            return Unauthorized();
        }

        return await RunTransitionAsync(
            () => transition(tripId, userId, cancellationToken));
    }

    private async Task<ActionResult<TripResult>> RunTransitionAsync(
        Func<Task<TripResult>> transition)
    {
        try
        {
            return Ok(await transition());
        }
        catch (TripNotFoundException)
        {
            return NotFound();
        }
        catch (TripAccessException)
        {
            return Forbid();
        }
        catch (TripStateConflictException exception)
        {
            return ConflictProblem("Trip state conflict", exception.Message);
        }
        catch (ActiveTripConflictException exception)
        {
            return ConflictProblem("Active trip conflict", exception.Message);
        }
        catch (DriverNotEligibleException exception)
        {
            return ConflictProblem("Driver is not eligible", exception.Message);
        }
        catch (DriverAvailabilityNotFoundException)
        {
            return ConflictProblem(
                "Driver is unavailable",
                "Go online before accepting a trip offer.");
        }
        catch (DriverAvailabilityConflictException exception)
        {
            return ConflictProblem("Driver availability conflict", exception.Message);
        }
        catch (TripOfferNotFoundException)
        {
            return NotFound();
        }
        catch (TripMatchingStateException exception)
        {
            return ConflictProblem("Trip matching conflict", exception.Message);
        }
    }

    private ConflictObjectResult ConflictProblem(string title, string detail)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = title,
            Detail = detail,
        });
    }

    private bool TryGetActor(out Guid userId, out UserRole role)
    {
        var roleValue = User.FindFirstValue("role");
        var hasUserId = Guid.TryParse(User.FindFirstValue("sub"), out userId);
        var hasRole = Enum.TryParse(roleValue, true, out role);
        return hasUserId && hasRole;
    }
}

public sealed record RequestTripRequest(
    [Required]
    [MaxLength(300)]
    string PickupAddress,
    [Range(-90, 90)]
    double PickupLatitude,
    [Range(-180, 180)]
    double PickupLongitude,
    [Required]
    [MaxLength(300)]
    string DestinationAddress,
    [Range(-90, 90)]
    double DestinationLatitude,
    [Range(-180, 180)]
    double DestinationLongitude,
    Guid FareQuoteId,
    RideCategory RideCategory);

public sealed record CancelTripRequest(
    [MaxLength(250)]
    string? Reason);
