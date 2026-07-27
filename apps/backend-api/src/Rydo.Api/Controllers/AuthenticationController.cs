using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Authentication;
using Rydo.Domain.Identity;

namespace Rydo.Api.Controllers;

[ApiController]
[EnableRateLimiting("api")]
[Route("api/v1/auth")]
public sealed class AuthenticationController(IAuthenticationService authentication) : ControllerBase
{
    [HttpPost("otp/request")]
    [EnableRateLimiting("otp-request")]
    [ProducesResponseType<OtpRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OtpRequestResult>> RequestOtp(
        RequestOtpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Role is not UserRole.Passenger and not UserRole.Driver)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(request.Role)] = ["Only Passenger or Driver can use phone sign-in."],
                }));
        }

        try
        {
            return Ok(await authentication.RequestOtpAsync(
                request.PhoneNumber,
                request.Role,
                cancellationToken));
        }
        catch (AuthenticationConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Phone number role conflict",
                Detail = exception.Message,
            });
        }
        catch (AuthenticationRateLimitException exception)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "OTP request limit reached",
                Detail = exception.Message,
            });
        }
    }

    [HttpPost("otp/verify")]
    [EnableRateLimiting("otp-verify")]
    [ProducesResponseType<TokenPairResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenPairResult>> VerifyOtp(
        VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authentication.VerifyOtpAsync(
            request.ChallengeId,
            request.Code,
            cancellationToken);

        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("otp-verify")]
    [ProducesResponseType<TokenPairResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenPairResult>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authentication.RefreshAsync(request.RefreshToken, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<AuthenticatedUser>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUser>> Me(CancellationToken cancellationToken)
    {
        if (!TryGetClaimGuid("sub", out var userId))
        {
            return Unauthorized();
        }

        var user = await authentication.GetCurrentUserAsync(userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [Authorize]
    [HttpPost("sessions/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeSession(CancellationToken cancellationToken)
    {
        if (!TryGetClaimGuid("sub", out var userId) ||
            !TryGetClaimGuid("sid", out var sessionId))
        {
            return Unauthorized();
        }

        await authentication.RevokeSessionAsync(userId, sessionId, cancellationToken);
        return NoContent();
    }

    private bool TryGetClaimGuid(string claimType, out Guid value)
    {
        return Guid.TryParse(User.FindFirstValue(claimType), out value);
    }
}

public sealed record RequestOtpRequest(
    [Required]
    [RegularExpression(@"^\+[1-9]\d{7,14}$")]
    string PhoneNumber,
    UserRole Role);

public sealed record VerifyOtpRequest(
    Guid ChallengeId,
    [Required]
    [RegularExpression(@"^\d{6}$")]
    string Code);

public sealed record RefreshTokenRequest(
    [Required]
    [MinLength(64)]
    string RefreshToken);
