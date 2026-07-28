namespace Rydo.Domain.Drivers;

public sealed class DriverDocument
{
    private DriverDocument()
    {
    }

    private DriverDocument(
        Guid id,
        Guid driverUserId,
        DriverDocumentType documentType,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        DateTimeOffset uploadedAt)
    {
        Id = id;
        DriverUserId = driverUserId;
        DocumentType = documentType;
        StorageObjectKey = storageObjectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        ReviewStatus = DriverDocumentReviewStatus.PendingReview;
        UploadedAt = uploadedAt;
    }

    public Guid Id { get; private set; }

    public Guid DriverUserId { get; private set; }

    public DriverDocumentType DocumentType { get; private set; }

    public string StorageObjectKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public DriverDocumentReviewStatus ReviewStatus { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? SupersededAt { get; private set; }

    public static DriverDocument Create(
        Guid id,
        Guid driverUserId,
        DriverDocumentType documentType,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        DateTimeOffset uploadedAt)
    {
        return new DriverDocument(
            id,
            driverUserId,
            documentType,
            storageObjectKey,
            originalFileName.Trim(),
            contentType.Trim().ToLowerInvariant(),
            sizeBytes,
            sha256.Trim().ToUpperInvariant(),
            uploadedAt);
    }

    public void Supersede(DateTimeOffset supersededAt)
    {
        if (ReviewStatus != DriverDocumentReviewStatus.Rejected)
        {
            throw new InvalidOperationException("Only a rejected driver document can be superseded.");
        }

        SupersededAt ??= supersededAt;
    }

    public void Approve(DateTimeOffset reviewedAt)
    {
        RequirePendingReview();
        ReviewStatus = DriverDocumentReviewStatus.Approved;
        ReviewedAt = reviewedAt;
        RejectionReason = null;
    }

    public void Reject(string reason, DateTimeOffset reviewedAt)
    {
        RequirePendingReview();
        ReviewStatus = DriverDocumentReviewStatus.Rejected;
        ReviewedAt = reviewedAt;
        RejectionReason = NormalizeReason(reason);
    }

    private void RequirePendingReview()
    {
        if (ReviewStatus != DriverDocumentReviewStatus.PendingReview)
        {
            throw new InvalidOperationException("Only a pending driver document can be reviewed.");
        }
    }

    private static string NormalizeReason(string reason)
    {
        var normalized = reason.Trim();
        return normalized.Length is > 0 and <= 500
            ? normalized
            : throw new ArgumentException(
                "Document rejection reasons must contain between 1 and 500 characters.");
    }
}
