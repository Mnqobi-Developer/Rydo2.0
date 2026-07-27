using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Passengers;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "passenger")]
[EnableRateLimiting("api")]
[Route("api/v1/passengers/me/profile")]
public sealed class PassengerProfilesController(
    IPassengerProfileService passengerProfiles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PassengerProfileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PassengerProfileResult>> Get(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await passengerProfiles.GetAsync(userId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    [ProducesResponseType<PassengerProfileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PassengerProfileResult>> Upsert(
        UpdatePassengerProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await passengerProfiles.UpsertAsync(
            userId,
            request.FirstName,
            request.LastName,
            request.Email,
            cancellationToken);

        return profile is null ? Forbid() : Ok(profile);
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue("sub"), out userId);
    }
}

public sealed record UpdatePassengerProfileRequest(
    [Required]
    [MaxLength(100)]
    string FirstName,
    [Required]
    [MaxLength(100)]
    string LastName,
    [EmailAddress]
    [MaxLength(254)]
    string? Email);
