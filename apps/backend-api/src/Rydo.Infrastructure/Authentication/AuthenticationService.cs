using Microsoft.EntityFrameworkCore;
using Rydo.Application.Authentication;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Authentication;

public sealed class AuthenticationService(
    RydoDbContext database,
    CryptoTokenService tokens,
    IOtpDeliveryService delivery,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<AuthenticationOptions> options)
    : IAuthenticationService
{
    private readonly AuthenticationOptions _options = options.Value;

    public async Task<OtpRequestResult> RequestOtpAsync(
        string phoneNumber,
        UserRole role,
        CancellationToken cancellationToken)
    {
        if (role is not UserRole.Passenger and not UserRole.Driver)
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Only passenger and driver sign-in is supported.");
        }

        var normalizedPhone = phoneNumber.Trim();
        var existingUser = await database.Users
            .SingleOrDefaultAsync(user => user.PhoneNumber == normalizedPhone, cancellationToken);

        if (existingUser is not null && existingUser.Role != role)
        {
            throw new AuthenticationConflictException(
                "This phone number is already registered with a different role.");
        }

        var now = timeProvider.GetUtcNow();
        var pendingChallenges = await database.OtpChallenges
            .Where(challenge => challenge.PhoneNumber == normalizedPhone && challenge.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        if (pendingChallenges.Any(challenge => challenge.CreatedAt > now.AddMinutes(-1)))
        {
            throw new AuthenticationRateLimitException(
                "Wait before requesting another code for this phone number.");
        }

        foreach (var pendingChallenge in pendingChallenges)
        {
            pendingChallenge.Cancel(now);
        }

        var challengeId = Guid.NewGuid();
        var code = CryptoTokenService.GenerateOtp();
        var expiresAt = now.AddMinutes(_options.OtpLifetimeMinutes);
        var challenge = OtpChallenge.Create(
            challengeId,
            normalizedPhone,
            role,
            tokens.HashOtp(challengeId, code),
            now,
            expiresAt,
            _options.OtpMaximumAttempts);

        database.OtpChallenges.Add(challenge);
        await database.SaveChangesAsync(cancellationToken);

        var developmentCode = await delivery.DeliverAsync(
            normalizedPhone,
            code,
            cancellationToken);

        return new OtpRequestResult(challengeId, expiresAt, developmentCode);
    }

    public async Task<TokenPairResult?> VerifyOtpAsync(
        Guid challengeId,
        string code,
        CancellationToken cancellationToken)
    {
        var challenge = await database.OtpChallenges
            .SingleOrDefaultAsync(item => item.Id == challengeId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (challenge is null || !challenge.CanVerify(now))
        {
            return null;
        }

        if (!tokens.VerifyOtp(challenge.Id, code, challenge.CodeHash))
        {
            challenge.RegisterFailedAttempt();
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }

        challenge.Consume(now);
        var user = await database.Users
            .SingleOrDefaultAsync(item => item.PhoneNumber == challenge.PhoneNumber, cancellationToken);

        if (user is null)
        {
            user = UserAccount.Create(challenge.PhoneNumber, challenge.RequestedRole, now);
            database.Users.Add(user);
        }

        if (!user.IsActive || user.Role != challenge.RequestedRole)
        {
            return null;
        }

        var session = AuthSession.Create(
            user,
            now,
            now.AddDays(_options.RefreshTokenDays));
        var refresh = tokens.CreateRefreshToken(session.Id, now);
        session.AddRefreshToken(SessionRefreshToken.Create(
            refresh.Id,
            session.Id,
            refresh.Hash,
            now,
            refresh.ExpiresAt));
        database.AuthSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);

        return CreateTokenPair(user, session.Id, refresh, now);
    }

    public async Task<TokenPairResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var tokenHash = CryptoTokenService.HashRefreshToken(refreshToken);
        var storedToken = await database.RefreshTokens
            .Include(token => token.Session)
            .ThenInclude(session => session.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return null;
        }

        var session = storedToken.Session;
        var now = timeProvider.GetUtcNow();

        if (storedToken.ConsumedAt is not null)
        {
            session.Revoke(now, "refresh_token_reuse");
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!storedToken.IsUsable(now) || !session.IsActive(now))
        {
            return null;
        }

        var replacement = tokens.CreateRefreshToken(session.Id, now);
        storedToken.Consume(now, replacement.Id);
        database.RefreshTokens.Add(SessionRefreshToken.Create(
            replacement.Id,
            session.Id,
            replacement.Hash,
            now,
            replacement.ExpiresAt));
        await database.SaveChangesAsync(cancellationToken);

        return CreateTokenPair(session.User, session.Id, replacement, now);
    }

    public async Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await database.AuthSessions.SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == userId,
            cancellationToken);

        if (session is null)
        {
            return false;
        }

        session.Revoke(timeProvider.GetUtcNow(), "user_sign_out");
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AuthenticatedUser?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await database.Users
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new AuthenticatedUser(user.Id, user.PhoneNumber, user.Role))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private TokenPairResult CreateTokenPair(
        UserAccount user,
        Guid sessionId,
        RefreshTokenMaterial refresh,
        DateTimeOffset now)
    {
        var access = tokens.CreateAccessToken(user, sessionId, now);
        return new TokenPairResult(
            access.Value,
            access.ExpiresAt,
            refresh.Value,
            refresh.ExpiresAt,
            new AuthenticatedUser(user.Id, user.PhoneNumber, user.Role));
    }
}
