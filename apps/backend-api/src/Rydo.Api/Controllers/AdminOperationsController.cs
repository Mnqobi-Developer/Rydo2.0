using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Admin;
using Rydo.Application.Payments;
using Rydo.Application.Trips;
using Rydo.Domain.Disputes;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Domain.Payments;
using Rydo.Domain.Trips;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[EnableRateLimiting("api")]
[Route("api/v1/admin")]
public sealed class AdminOperationsController(
    IAdminOperationsService operations) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<AdminOverviewResult>> GetOverview(
        CancellationToken cancellationToken)
    {
        return Ok(await operations.GetOverviewAsync(cancellationToken));
    }

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserResult>>> ListUsers(
        UserRole? role,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await operations.ListUsersAsync(
            role,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("drivers")]
    public async Task<ActionResult<PagedResult<AdminDriverResult>>> ListDrivers(
        DriverOnboardingStatus? status,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await operations.ListDriversAsync(
            status,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("drivers/{driverUserId:guid}")]
    public async Task<ActionResult<AdminDriverResult>> GetDriver(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var driver = await operations.GetDriverAsync(driverUserId, cancellationToken);
        return driver is null ? NotFound() : Ok(driver);
    }

    [HttpPost("drivers/{driverUserId:guid}/review")]
    public async Task<ActionResult<AdminDriverResult>> ReviewDriver(
        Guid driverUserId,
        ReviewDriverRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminUserId))
        {
            return Unauthorized();
        }

        return await RunMutationAsync(() => operations.ReviewDriverAsync(
            adminUserId,
            driverUserId,
            request.Approve,
            request.Reason,
            cancellationToken));
    }

    [HttpGet("trips")]
    public async Task<ActionResult<PagedResult<TripResult>>> ListTrips(
        TripStatus? status,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await operations.ListTripsAsync(
            status,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("payments")]
    public async Task<ActionResult<PagedResult<PaymentResult>>> ListPayments(
        PaymentStatus? status,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await operations.ListPaymentsAsync(
            status,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("drivers/live")]
    public async Task<ActionResult<IReadOnlyList<AdminLiveDriverResult>>> ListLiveDrivers(
        CancellationToken cancellationToken)
    {
        return Ok(await operations.ListLiveDriversAsync(cancellationToken));
    }

    [HttpGet("disputes")]
    public async Task<ActionResult<PagedResult<AdminDisputeResult>>> ListDisputes(
        DisputeStatus? status,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await operations.ListDisputesAsync(
            status,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpPost("disputes/{disputeId:guid}/review")]
    public async Task<ActionResult<AdminDisputeResult>> ReviewDispute(
        Guid disputeId,
        ReviewDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminUserId))
        {
            return Unauthorized();
        }

        return await RunMutationAsync(() => operations.ReviewDisputeAsync(
            adminUserId,
            disputeId,
            request.Status,
            request.Resolution,
            cancellationToken));
    }

    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AdminAuditResult>>> ListAudit(
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await operations.ListAuditAsync(page, pageSize, cancellationToken));
    }

    private async Task<ActionResult<T>> RunMutationAsync<T>(Func<Task<T>> mutation)
    {
        try
        {
            return Ok(await mutation());
        }
        catch (AdminResourceNotFoundException)
        {
            return NotFound();
        }
        catch (AdminOperationValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid admin operation",
                Detail = exception.Message,
            });
        }
        catch (AdminOperationConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Admin operation conflict",
                Detail = exception.Message,
            });
        }
    }

    private bool TryGetAdminId(out Guid adminUserId)
    {
        return Guid.TryParse(User.FindFirstValue("sub"), out adminUserId);
    }
}

public sealed record ReviewDriverRequest(
    bool Approve,
    [MaxLength(500)]
    string? Reason);

public sealed record ReviewDisputeRequest(
    DisputeStatus Status,
    [MaxLength(2000)]
    string? Resolution);
