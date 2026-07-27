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
[Route("api/v1/drivers/me/vehicle")]
public sealed class DriverVehiclesController(IDriverVehicleService driverVehicles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DriverVehicleResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverVehicleResult>> Get(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var vehicle = await driverVehicles.GetAsync(userId, cancellationToken);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [HttpPut]
    [ProducesResponseType<DriverVehicleResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverVehicleResult>> Upsert(
        UpdateDriverVehicleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var vehicle = await driverVehicles.UpsertAsync(
                userId,
                request.Make,
                request.Model,
                request.Year,
                request.Color,
                request.RegistrationNumber,
                request.VehicleIdentificationNumber,
                request.SeatCapacity,
                cancellationToken);

            return vehicle is null ? Forbid() : Ok(vehicle);
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
        catch (DriverVehicleValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid vehicle information",
                Detail = exception.Message,
            });
        }
        catch (DriverVehicleConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Driver vehicle conflict",
                Detail = exception.Message,
            });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue("sub"), out userId);
    }
}

public sealed record UpdateDriverVehicleRequest(
    [Required]
    [MaxLength(100)]
    string Make,
    [Required]
    [MaxLength(100)]
    string Model,
    [Range(1980, 2100)]
    int Year,
    [Required]
    [MaxLength(50)]
    string Color,
    [Required]
    [RegularExpression(@"^[A-Za-z0-9 -]{2,16}$")]
    string RegistrationNumber,
    [Required]
    [RegularExpression(@"^[A-HJ-NPR-Za-hj-npr-z0-9]{17}$")]
    string VehicleIdentificationNumber,
    [Range(1, 16)]
    int SeatCapacity);
