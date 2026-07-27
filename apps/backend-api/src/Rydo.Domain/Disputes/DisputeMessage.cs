namespace Rydo.Domain.Disputes;

public sealed class DisputeMessage
{
    private DisputeMessage()
    {
    }

    private DisputeMessage(
        Guid id,
        Guid disputeId,
        Guid authorUserId,
        string body,
        DateTimeOffset createdAt)
    {
        Id = id;
        DisputeId = disputeId;
        AuthorUserId = authorUserId;
        Body = NormalizeBody(body);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid DisputeId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static DisputeMessage Create(
        Guid disputeId,
        Guid authorUserId,
        string body,
        DateTimeOffset createdAt)
    {
        if (disputeId == Guid.Empty || authorUserId == Guid.Empty)
        {
            throw new ArgumentException("Dispute message identifiers are required.");
        }

        return new DisputeMessage(Guid.NewGuid(), disputeId, authorUserId, body, createdAt);
    }

    private static string NormalizeBody(string body)
    {
        var normalized = body.Trim();

        if (normalized.Length is 0 or > 2000)
        {
            throw new ArgumentException("Dispute messages must contain between 1 and 2000 characters.");
        }

        return normalized;
    }
}
