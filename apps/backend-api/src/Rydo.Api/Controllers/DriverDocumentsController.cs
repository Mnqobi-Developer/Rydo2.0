using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rydo.Application.Drivers;
using Rydo.Domain.Drivers;

namespace Rydo.Api.Controllers;

[ApiController]
[Authorize(Roles = "driver")]
[EnableRateLimiting("api")]
[Route("api/v1/drivers/me/documents")]
public sealed class DriverDocumentsController(IDriverDocumentService driverDocuments) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DriverDocumentResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DriverDocumentResult>>> List(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var documents = await driverDocuments.ListAsync(userId, cancellationToken);
        return documents is null ? Forbid() : Ok(documents);
    }

    [HttpGet("{documentId:guid}")]
    [ProducesResponseType<DriverDocumentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverDocumentResult>> Get(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var document = await driverDocuments.GetAsync(userId, documentId, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost]
    [ProducesResponseType<DriverDocumentResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverDocumentResult>> Register(
        RegisterDriverDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!Enum.IsDefined(request.DocumentType))
        {
            ModelState.AddModelError(
                nameof(request.DocumentType),
                "Select a supported driver document type.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var document = await driverDocuments.RegisterAsync(
                userId,
                request.DocumentType,
                request.OriginalFileName,
                request.ContentType,
                request.SizeBytes,
                request.Sha256,
                cancellationToken);

            return document is null
                ? Forbid()
                : CreatedAtAction(nameof(Get), new { documentId = document.Id }, document);
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
        catch (DriverDocumentConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Driver document conflict",
                Detail = exception.Message,
            });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue("sub"), out userId);
    }
}

public sealed record RegisterDriverDocumentRequest(
    DriverDocumentType DocumentType,
    [Required]
    [MaxLength(255)]
    [RegularExpression(@"^[^\\/]+$")]
    string OriginalFileName,
    [Required]
    [RegularExpression(@"^(application/pdf|image/jpeg|image/png)$")]
    string ContentType,
    [Range(1, 10 * 1024 * 1024)]
    long SizeBytes,
    [Required]
    [RegularExpression(@"^[A-Fa-f0-9]{64}$")]
    string Sha256);
