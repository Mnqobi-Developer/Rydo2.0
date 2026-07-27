using Microsoft.EntityFrameworkCore;
using Rydo.Application.Drivers;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Drivers;

public sealed class DriverDocumentService(
    RydoDbContext database,
    TimeProvider timeProvider) : IDriverDocumentService
{
    public async Task<IReadOnlyList<DriverDocumentResult>?> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        return await database.DriverDocuments
            .Where(document => document.DriverUserId == userId && document.SupersededAt == null)
            .OrderBy(document => document.DocumentType)
            .Select(document => new DriverDocumentResult(
                document.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256,
                document.ReviewStatus,
                document.UploadedAt,
                document.ReviewedAt,
                document.RejectionReason))
            .ToListAsync(cancellationToken);
    }

    public async Task<DriverDocumentResult?> GetAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        return await database.DriverDocuments
            .Where(document =>
                document.Id == documentId &&
                document.DriverUserId == userId &&
                document.SupersededAt == null)
            .Select(document => new DriverDocumentResult(
                document.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256,
                document.ReviewStatus,
                document.UploadedAt,
                document.ReviewedAt,
                document.RejectionReason))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<DriverDocumentResult?> RegisterAsync(
        Guid userId,
        DriverDocumentType documentType,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        var profile = await database.DriverProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId,
            cancellationToken) ?? throw new DriverProfileNotFoundException();

        if (!profile.CanEdit)
        {
            throw new DriverDocumentConflictException(
                "Driver documents cannot change while onboarding is under review or approved.");
        }

        var hasCurrentDocument = await database.DriverDocuments.AnyAsync(
            document => document.DriverUserId == userId &&
                document.DocumentType == documentType &&
                document.SupersededAt == null &&
                document.ReviewStatus != DriverDocumentReviewStatus.Rejected,
            cancellationToken);

        if (hasCurrentDocument)
        {
            throw new DriverDocumentConflictException(
                $"A current {documentType} document already exists for this driver.");
        }

        var rejectedDocuments = await database.DriverDocuments
            .Where(document => document.DriverUserId == userId &&
                document.DocumentType == documentType &&
                document.SupersededAt == null &&
                document.ReviewStatus == DriverDocumentReviewStatus.Rejected)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var rejectedDocument in rejectedDocuments)
        {
            rejectedDocument.Supersede(now);
        }

        var id = Guid.NewGuid();
        var document = DriverDocument.Create(
            id,
            userId,
            documentType,
            CreateStorageObjectKey(userId, id, contentType),
            originalFileName,
            contentType,
            sizeBytes,
            sha256,
            now);
        database.DriverDocuments.Add(document);
        await database.SaveChangesAsync(cancellationToken);

        return ToResult(document);
    }

    private Task<bool> IsActiveDriverAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return database.Users.AnyAsync(
            user => user.Id == userId &&
                user.IsActive &&
                user.Role == UserRole.Driver,
            cancellationToken);
    }

    private static string CreateStorageObjectKey(
        Guid userId,
        Guid documentId,
        string contentType)
    {
        var extension = contentType.Trim().ToLowerInvariant() switch
        {
            "application/pdf" => "pdf",
            "image/jpeg" => "jpg",
            "image/png" => "png",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType)),
        };

        return $"driver-documents/{userId:N}/{documentId:N}.{extension}";
    }

    private static DriverDocumentResult ToResult(DriverDocument document)
    {
        return new DriverDocumentResult(
            document.Id,
            document.DocumentType,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Sha256,
            document.ReviewStatus,
            document.UploadedAt,
            document.ReviewedAt,
            document.RejectionReason);
    }
}
