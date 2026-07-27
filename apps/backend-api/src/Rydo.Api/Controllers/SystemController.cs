using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.System;

namespace Rydo.Api.Controllers;

[ApiController]
[EnableRateLimiting("api")]
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ServiceStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<ServiceStatusResponse> Get()
    {
        return Ok(new ServiceStatusResponse("RYDO API", "foundation", "operational"));
    }
}
