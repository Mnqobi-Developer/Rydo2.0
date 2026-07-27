using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Drivers;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "driver")]
[EnableRateLimiting("api")]
[Route("api/v1/drivers/me")]
public sealed class DriverProfilesController(IDriverProfileService driverProfiles) : ControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType<DriverProfileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverProfileResult>> Get(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await driverProfiles.GetAsync(userId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profile")]
    [ProducesResponseType<DriverProfileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverProfileResult>> Upsert(
        UpdateDriverProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var profile = await driverProfiles.UpsertAsync(
                userId,
                request.FirstName,
                request.LastName,
                request.Email,
                cancellationToken);

            return profile is null ? Forbid() : Ok(profile);
        }
        catch (DriverOnboardingStateException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    [HttpPost("onboarding/submit")]
    [ProducesResponseType<DriverProfileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverProfileResult>> SubmitOnboarding(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var profile = await driverProfiles.SubmitAsync(userId, cancellationToken);
            return profile is null ? Forbid() : Ok(profile);
        }
        catch (DriverProfileNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Driver profile not found",
                Detail = exception.Message,
            });
        }
        catch (DriverOnboardingDocumentsMissingException exception)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Required driver documents are missing",
                Detail = exception.Message,
            };
            problem.Extensions["missingDocumentTypes"] = exception.MissingDocumentTypes;
            return Conflict(problem);
        }
        catch (DriverOnboardingVehicleMissingException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Driver vehicle is missing",
                Detail = exception.Message,
            });
        }
        catch (DriverOnboardingStateException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    private ConflictObjectResult ConflictProblem(string detail)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Driver onboarding state conflict",
            Detail = detail,
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue("sub"), out userId);
    }
}

public sealed record UpdateDriverProfileRequest(
    [Required]
    [MaxLength(100)]
    string FirstName,
    [Required]
    [MaxLength(100)]
    string LastName,
    [EmailAddress]
    [MaxLength(254)]
    string? Email);
