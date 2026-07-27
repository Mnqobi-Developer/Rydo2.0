namespace Rydo.Domain.Disputes;

public sealed class Dispute
{
    private Dispute()
    {
    }

    private Dispute(
        Guid id,
        Guid tripId,
        Guid openedByUserId,
        DisputeCategory category,
        string subject,
        string description,
        DateTimeOffset createdAt)
    {
        Id = id;
        TripId = tripId;
        OpenedByUserId = openedByUserId;
        Category = category;
        Subject = NormalizeRequired(subject, 120, "Dispute subjects");
        Description = NormalizeRequired(description, 2000, "Dispute descriptions");
        Status = DisputeStatus.Open;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid TripId { get; private set; }

    public Guid OpenedByUserId { get; private set; }

    public DisputeCategory Category { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public DisputeStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public string? Resolution { get; private set; }

    public int Version { get; private set; }

    public bool AcceptsMessages => Status is DisputeStatus.Open or DisputeStatus.UnderReview;

    public static Dispute Open(
        Guid tripId,
        Guid openedByUserId,
        DisputeCategory category,
        string subject,
        string description,
        DateTimeOffset createdAt)
    {
        if (tripId == Guid.Empty || openedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Dispute identifiers are required.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), "Select a valid dispute category.");
        }

        return new Dispute(
            Guid.NewGuid(),
            tripId,
            openedByUserId,
            category,
            subject,
            description,
            createdAt);
    }

    public void MarkUnderReview(DateTimeOffset updatedAt)
    {
        RequireStatus(DisputeStatus.Open, "mark under review");
        Status = DisputeStatus.UnderReview;
        Touch(updatedAt);
    }

    public void RecordMessage(DateTimeOffset updatedAt)
    {
        if (!AcceptsMessages)
        {
            throw new InvalidOperationException("A closed dispute cannot accept messages.");
        }

        Touch(updatedAt);
    }

    public void Resolve(Guid adminUserId, string resolution, DateTimeOffset resolvedAt)
    {
        Complete(DisputeStatus.Resolved, adminUserId, resolution, resolvedAt);
    }

    public void Reject(Guid adminUserId, string resolution, DateTimeOffset resolvedAt)
    {
        Complete(DisputeStatus.Rejected, adminUserId, resolution, resolvedAt);
    }

    private void Complete(
        DisputeStatus status,
        Guid adminUserId,
        string resolution,
        DateTimeOffset resolvedAt)
    {
        if (!AcceptsMessages)
        {
            throw new InvalidOperationException("A closed dispute cannot be changed.");
        }

        if (adminUserId == Guid.Empty)
        {
            throw new ArgumentException("A resolving administrator is required.");
        }

        Resolution = NormalizeRequired(resolution, 2000, "Dispute resolutions");
        Status = status;
        ResolvedByUserId = adminUserId;
        ResolvedAt = resolvedAt;
        Touch(resolvedAt);
    }

    private void RequireStatus(DisputeStatus requiredStatus, string action)
    {
        if (Status != requiredStatus)
        {
            throw new InvalidOperationException($"A dispute in the {Status} state cannot {action}.");
        }
    }

    private void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
        Version++;
    }

    private static string NormalizeRequired(string value, int maximumLength, string fieldName)
    {
        var normalized = value.Trim();

        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{fieldName} must contain between 1 and {maximumLength} characters.");
        }

        return normalized;
    }
}
