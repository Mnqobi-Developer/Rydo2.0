using Rydo.Domain.Drivers;

namespace Rydo.Application.Drivers;

public sealed record DriverDocumentResult(
    Guid Id,
    DriverDocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DriverDocumentReviewStatus ReviewStatus,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

public interface IDriverDocumentService
{
    Task<IReadOnlyList<DriverDocumentResult>?> ListAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<DriverDocumentResult?> GetAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<DriverDocumentResult?> RegisterAsync(
        Guid userId,
        DriverDocumentType documentType,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        CancellationToken cancellationToken);
}

public sealed class DriverDocumentConflictException(string message) : Exception(message);

public sealed class DriverOnboardingDocumentsMissingException(
    IReadOnlyList<DriverDocumentType> missingDocumentTypes)
    : Exception("Upload all required driver documents before submitting onboarding for review.")
{
    public IReadOnlyList<DriverDocumentType> MissingDocumentTypes { get; } = missingDocumentTypes;
}
