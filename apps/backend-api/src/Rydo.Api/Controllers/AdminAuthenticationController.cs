using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Admin;
using Rydo.Application.Authentication;

namespace Rydo.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/admin/auth")]
public sealed class AdminAuthenticationController(
    IAdminAuthenticationService authentication) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("admin-login")]
    public async Task<ActionResult<TokenPairResult>> Login(
        AdminLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await authentication.LoginAsync(
                request.Email,
                request.Password,
                cancellationToken);
            return result is null ? Unauthorized() : Ok(result);
        }
        catch (AdminAccessUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Admin access is unavailable",
                Detail = "Admin bootstrap credentials have not been configured for this environment.",
            });
        }
    }
}

public sealed record AdminLoginRequest(
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    string Email,
    [Required]
    [MinLength(16)]
    [MaxLength(200)]
    string Password);
