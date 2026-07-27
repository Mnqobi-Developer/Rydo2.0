using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Ratings;
using Rydo.Domain.Identity;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "passenger,driver")]
[EnableRateLimiting("api")]
public sealed class RatingsController(IRatingService ratings) : ControllerBase
{
    [HttpPost("api/v1/trips/{tripId:guid}/ratings")]
    public async Task<ActionResult<RatingResult>> Create(Guid tripId, CreateRatingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role)) return Unauthorized();
        try
        {
            var existing = await ratings.GetOwnForTripAsync(tripId, userId, role, cancellationToken);
            var rating = await ratings.CreateAsync(tripId, userId, role, request.Score, request.Comment, cancellationToken);
            return existing is null ? CreatedAtAction(nameof(GetOwnForTrip), new { tripId }, rating) : Ok(rating);
        }
        catch (RatingNotFoundException) { return NotFound(); }
        catch (RatingAccessException) { return Forbid(); }
        catch (RatingValidationException exception) { return BadRequestProblem(exception.Message); }
        catch (RatingStateConflictException exception)
        {
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Rating conflict", Detail = exception.Message });
        }
    }

    [HttpGet("api/v1/trips/{tripId:guid}/ratings/me")]
    public async Task<ActionResult<RatingResult>> GetOwnForTrip(Guid tripId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role)) return Unauthorized();
        try
        {
            var rating = await ratings.GetOwnForTripAsync(tripId, userId, role, cancellationToken);
            return rating is null ? NotFound() : Ok(rating);
        }
        catch (RatingNotFoundException) { return NotFound(); }
        catch (RatingAccessException) { return Forbid(); }
    }

    [HttpGet("api/v1/ratings/me/summary")]
    public async Task<ActionResult<RatingSummaryResult>> GetOwnSummary(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role)) return Unauthorized();
        try { return Ok(await ratings.GetOwnSummaryAsync(userId, role, cancellationToken)); }
        catch (RatingAccessException) { return Forbid(); }
    }

    [HttpGet("api/v1/drivers/{driverUserId:guid}/ratings/summary")]
    public async Task<ActionResult<RatingSummaryResult>> GetDriverSummary(Guid driverUserId, CancellationToken cancellationToken)
    {
        try { return Ok(await ratings.GetDriverSummaryAsync(driverUserId, cancellationToken)); }
        catch (RatedUserNotFoundException) { return NotFound(); }
    }

    private BadRequestObjectResult BadRequestProblem(string detail) => BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Invalid rating", Detail = detail });

    private bool TryGetActor(out Guid userId, out UserRole role)
    {
        var roleValue = User.FindFirstValue("role");
        var hasUserId = Guid.TryParse(User.FindFirstValue("sub"), out userId);
        var hasRole = Enum.TryParse(roleValue, true, out role);
        return hasUserId && hasRole;
    }
}

public sealed record CreateRatingRequest([Range(1, 5)] int Score, [MaxLength(500)] string? Comment);
