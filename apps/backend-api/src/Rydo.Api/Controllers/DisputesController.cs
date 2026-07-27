using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Disputes;
using Rydo.Domain.Disputes;
using Rydo.Domain.Identity;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "passenger,driver")]
[EnableRateLimiting("api")]
public sealed class DisputesController(IDisputeService disputes) : ControllerBase
{
    [HttpPost("api/v1/trips/{tripId:guid}/disputes")]
    public async Task<ActionResult<DisputeDetailsResult>> Open(
        Guid tripId,
        OpenDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var result = await disputes.OpenAsync(
                tripId,
                userId,
                role,
                request.Category,
                request.Subject,
                request.Description,
                cancellationToken);

            return result.Created
                ? CreatedAtAction(nameof(Get), new { disputeId = result.Dispute.Id }, result.Dispute)
                : Ok(result.Dispute);
        }
        catch (DisputeTripNotFoundException)
        {
            return NotFound();
        }
        catch (DisputeAccessException)
        {
            return Forbid();
        }
        catch (DisputeValidationException exception)
        {
            return BadRequestProblem(exception.Message);
        }
        catch (DisputeStateConflictException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    [HttpGet("api/v1/disputes/me")]
    public async Task<ActionResult<IReadOnlyList<DisputeSummaryResult>>> List(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        return Ok(await disputes.ListAsync(userId, role, cancellationToken));
    }

    [HttpGet("api/v1/disputes/{disputeId:guid}")]
    public async Task<ActionResult<DisputeDetailsResult>> Get(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        var dispute = await disputes.GetAsync(
            disputeId,
            userId,
            role,
            cancellationToken);
        return dispute is null ? NotFound() : Ok(dispute);
    }

    [HttpPost("api/v1/disputes/{disputeId:guid}/messages")]
    public async Task<ActionResult<DisputeMessageResult>> AddMessage(
        Guid disputeId,
        AddDisputeMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var message = await disputes.AddMessageAsync(
                disputeId,
                userId,
                role,
                request.Body,
                cancellationToken);
            return CreatedAtAction(nameof(Get), new { disputeId }, message);
        }
        catch (DisputeAccessException)
        {
            return Forbid();
        }
        catch (DisputeValidationException exception)
        {
            return BadRequestProblem(exception.Message);
        }
        catch (DisputeStateConflictException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    private BadRequestObjectResult BadRequestProblem(string detail)
    {
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid dispute",
            Detail = detail,
        });
    }

    private ConflictObjectResult ConflictProblem(string detail)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Dispute conflict",
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

public sealed record OpenDisputeRequest(
    DisputeCategory Category,
    [Required]
    [MaxLength(120)]
    string Subject,
    [Required]
    [MaxLength(2000)]
    string Description);

public sealed record AddDisputeMessageRequest(
    [Required]
    [MaxLength(2000)]
    string Body);
