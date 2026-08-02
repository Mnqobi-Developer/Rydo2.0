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
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType<DriverDocumentResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverDocumentResult>> Register(
        [FromForm] UploadDriverDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!Enum.IsDefined(request.DocumentType) || request.File is null)
        {
            ModelState.AddModelError(
                nameof(request.DocumentType),
                "Select a supported driver document type.");
            return ValidationProblem(ModelState);
        }

        if (request.File.Length is < 1 or > 10 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(request.File),
                "Documents must contain data and be no larger than 10 MB.");
        }
        if (request.File.ContentType is not ("application/pdf" or "image/jpeg" or "image/png"))
        {
            ModelState.AddModelError(nameof(request.File),
                "Choose a PDF, JPEG, or PNG document.");
        }
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            await using var content = request.File.OpenReadStream();
            var document = await driverDocuments.UploadAsync(
                userId,
                request.DocumentType,
                Path.GetFileName(request.File.FileName),
                request.File.ContentType,
                content,
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
        catch (DriverDocumentStorageException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Document storage unavailable",
                Detail = exception.Message,
            });
        }
    }

    [HttpGet("{documentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Download(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            var result = await driverDocuments.OpenContentAsync(
                userId,
                documentId,
                cancellationToken);
            return result is null
                ? NotFound()
                : File(
                    result.Content,
                    result.Document.ContentType,
                    result.Document.OriginalFileName);
        }
        catch (DriverDocumentStorageException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Document storage unavailable",
                Detail = exception.Message,
            });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue("sub"), out userId);
    }
}

public sealed class UploadDriverDocumentRequest
{
    [Required]
    public DriverDocumentType DocumentType { get; init; }

    [Required]
    public IFormFile File { get; init; } = null!;
}
