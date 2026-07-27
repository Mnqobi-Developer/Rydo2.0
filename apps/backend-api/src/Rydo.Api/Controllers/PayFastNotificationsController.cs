using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Payments;

namespace Rydo.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("payment-callback")]
[Route("api/v1/payments/payfast")]
public sealed class PayFastNotificationsController(IPaymentService payments) : ControllerBase
{
    [HttpPost("notify")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Notify(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest();
        }

        var form = await Request.ReadFormAsync(cancellationToken);

        if (form.Any(field => field.Value.Count != 1))
        {
            return BadRequest();
        }

        var fields = form
            .Select(field => new KeyValuePair<string, string>(
                field.Key,
                field.Value[0] ?? string.Empty))
            .ToList();

        await payments.ProcessPayFastNotificationAsync(
            fields,
            HttpContext.Connection.RemoteIpAddress,
            cancellationToken);

        return Ok();
    }
}
