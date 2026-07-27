using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Payments;
using Rydo.Domain.Identity;
using Rydo.Domain.Payments;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "passenger,driver")]
[EnableRateLimiting("api")]
[Route("api/v1")]
public sealed class PaymentsController(IPaymentService payments) : ControllerBase
{
    [HttpPost("trips/{tripId:guid}/payments")]
    [Authorize(Roles = "passenger")]
    [ProducesResponseType<CreatePaymentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CreatePaymentResult>> Create(
        Guid tripId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out _))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await payments.CreateAsync(
                tripId,
                userId,
                request.Method,
                cancellationToken));
        }
        catch (PaymentNotFoundException)
        {
            return NotFound();
        }
        catch (PaymentAccessException)
        {
            return Forbid();
        }
        catch (TripFareNotFinalizedException exception)
        {
            return ConflictProblem("Trip fare is not finalized", exception.Message);
        }
        catch (PaymentConflictException exception)
        {
            return ConflictProblem("Payment conflict", exception.Message);
        }
        catch (PaymentProviderUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Payment provider unavailable",
                    Detail = exception.Message,
                });
        }
    }

    [HttpGet("trips/{tripId:guid}/payment")]
    [ProducesResponseType<PaymentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResult>> Get(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role))
        {
            return Unauthorized();
        }

        var payment = await payments.GetForTripAsync(
            tripId,
            userId,
            role,
            cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost("payments/{paymentId:guid}/cash/confirm")]
    [Authorize(Roles = "driver")]
    [ProducesResponseType<PaymentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResult>> ConfirmCash(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out _))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await payments.ConfirmCashAsync(
                paymentId,
                userId,
                cancellationToken));
        }
        catch (PaymentNotFoundException)
        {
            return NotFound();
        }
        catch (PaymentAccessException)
        {
            return Forbid();
        }
        catch (PaymentConflictException exception)
        {
            return ConflictProblem("Payment conflict", exception.Message);
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
        var hasUserId = Guid.TryParse(User.FindFirstValue("sub"), out userId);
        var hasRole = Enum.TryParse(User.FindFirstValue("role"), true, out role);
        return hasUserId && hasRole;
    }
}

public sealed record CreatePaymentRequest(PaymentMethod Method);
