using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Rydo.Application.Drivers;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Drivers;

public sealed class DriverDocumentService(
    RydoDbContext database,
    TimeProvider timeProvider,
    IDriverDocumentObjectStorage storage) : IDriverDocumentService
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

    public async Task<DriverDocumentResult?> UploadAsync(
        Guid userId,
        DriverDocumentType documentType,
        string originalFileName,
        string contentType,
        Stream content,
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

        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        if (normalizedContentType is not ("application/pdf" or "image/jpeg" or "image/png"))
        {
            throw new DriverDocumentConflictException("Choose a PDF, JPEG, or PNG document.");
        }

        await using var bufferedContent = new MemoryStream();
        await content.CopyToAsync(bufferedContent, cancellationToken);
        if (bufferedContent.Length is < 1 or > 10 * 1024 * 1024)
        {
            throw new DriverDocumentConflictException("Documents must contain data and be no larger than 10 MB.");
        }
        if (!HasExpectedSignature(bufferedContent.GetBuffer(), bufferedContent.Length, normalizedContentType))
        {
            throw new DriverDocumentConflictException(
                "The file contents do not match the selected PDF, JPEG, or PNG type.");
        }
        var sha256 = Convert.ToHexString(SHA256.HashData(bufferedContent.GetBuffer().AsSpan(
            0,
            checked((int)bufferedContent.Length))));

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

        if (profile.OnboardingStatus == DriverOnboardingStatus.Rejected)
        {
            profile.BeginCorrection(now);
        }

        var id = Guid.NewGuid();
        var objectKey = CreateStorageObjectKey(userId, id, normalizedContentType);
        var document = DriverDocument.Create(
            id,
            userId,
            documentType,
            objectKey,
            originalFileName,
            normalizedContentType,
            bufferedContent.Length,
            sha256,
            now);

        try
        {
            bufferedContent.Position = 0;
            await storage.UploadAsync(
                objectKey,
                normalizedContentType,
                bufferedContent,
                cancellationToken);
            database.DriverDocuments.Add(document);
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DriverDocumentConflictException)
        {
            throw;
        }
        catch (Exception exception)
        {
            try
            {
                await storage.DeleteAsync(objectKey, CancellationToken.None);
            }
            catch
            {
                // Preserve the upload/database failure; orphan cleanup is safe to retry operationally.
            }
            throw new DriverDocumentStorageException(
                "The protected document could not be stored. Try again.",
                exception);
        }

        return ToResult(document);
    }

    public async Task<DriverDocumentContentResult?> OpenContentAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken)) return null;

        var document = await database.DriverDocuments.SingleOrDefaultAsync(
            item => item.Id == documentId &&
                item.DriverUserId == userId &&
                item.SupersededAt == null,
            cancellationToken);
        if (document is null) return null;

        try
        {
            var content = await storage.OpenReadAsync(document.StorageObjectKey, cancellationToken);
            return new DriverDocumentContentResult(ToResult(document), content);
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            throw new DriverDocumentStorageException(
                "The protected document is temporarily unavailable.",
                exception);
        }
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

    private static bool HasExpectedSignature(byte[] bytes, long length, string contentType)
    {
        ReadOnlySpan<byte> signature = contentType switch
        {
            "application/pdf" => "%PDF-"u8,
            "image/jpeg" => [0xFF, 0xD8, 0xFF],
            "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            _ => [],
        };
        return length >= signature.Length && bytes.AsSpan(0, signature.Length).SequenceEqual(signature);
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
