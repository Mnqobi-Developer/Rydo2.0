using Microsoft.AspNetCore.Mvc;
using Rydo.Application.System;

namespace Rydo.Api.Controllers;

[ApiController]
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
