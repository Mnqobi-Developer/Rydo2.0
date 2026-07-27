namespace Rydo.Domain.Identity;

public sealed class AuthSession
{
    private readonly List<SessionRefreshToken> _refreshTokens = [];

    private AuthSession()
    {
    }

    private AuthSession(Guid id, UserAccount user, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        User = user;
        UserId = user.Id;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public UserAccount User { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevocationReason { get; private set; }

    public IReadOnlyCollection<SessionRefreshToken> RefreshTokens => _refreshTokens;

    public bool IsActive(DateTimeOffset now)
    {
        return RevokedAt is null && ExpiresAt > now && User.IsActive;
    }

    public void AddRefreshToken(SessionRefreshToken refreshToken)
    {
        _refreshTokens.Add(refreshToken);
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        RevokedAt ??= now;
        RevocationReason ??= reason;
    }

    public static AuthSession Create(UserAccount user, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        return new AuthSession(Guid.NewGuid(), user, createdAt, expiresAt);
    }
}
