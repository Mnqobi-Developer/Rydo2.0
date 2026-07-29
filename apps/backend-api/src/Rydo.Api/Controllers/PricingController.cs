using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Maps;
using Rydo.Application.Pricing;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "passenger")]
[EnableRateLimiting("pricing")]
[Route("api/v1/pricing")]
public sealed class PricingController(IPricingService pricing) : ControllerBase
{
    [HttpPost("quotes")]
    [ProducesResponseType<FareQuoteResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FareQuoteResult>> CreateQuote(
        CreateFareQuoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var passengerUserId))
        {
            return Unauthorized();
        }

        try
        {
            var quote = await pricing.CreateQuoteAsync(
                passengerUserId, request.Pickup, request.Destination, cancellationToken);
            return Created($"/api/v1/pricing/quotes/{quote.Id}", quote);
        }
        catch (PricingValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid fare quote request",
                Detail = exception.Message,
            });
        }
        catch (FareRouteNotFoundException)
        {
            return NotFound();
        }
        catch (MapProviderUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Pricing route unavailable",
                detail: exception.Message);
        }
    }
}

public sealed record CreateFareQuoteRequest(
    GeoCoordinate Pickup,
    GeoCoordinate Destination);
