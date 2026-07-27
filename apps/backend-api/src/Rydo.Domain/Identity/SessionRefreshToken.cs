namespace Rydo.Domain.Identity;

public sealed class SessionRefreshToken
{
    private SessionRefreshToken()
    {
    }

    private SessionRefreshToken(
        Guid id,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public AuthSession Session { get; private set; } = null!;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsUsable(DateTimeOffset now)
    {
        return ConsumedAt is null && ExpiresAt > now;
    }

    public void Consume(DateTimeOffset now, Guid replacementTokenId)
    {
        ConsumedAt = now;
        ReplacedByTokenId = replacementTokenId;
    }

    public static SessionRefreshToken Create(
        Guid id,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        return new SessionRefreshToken(id, sessionId, tokenHash, createdAt, expiresAt);
    }
}
