using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rydo.Application.Admin;
using Rydo.Application.Authentication;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Authentication;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Admin;

public sealed class AdminAuthenticationService(
    RydoDbContext database,
    CryptoTokenService tokens,
    TimeProvider timeProvider,
    IOptions<AuthenticationOptions> authenticationOptions,
    IOptions<AdminAccessOptions> adminOptions) : IAdminAuthenticationService
{
    private static readonly string DummyPasswordHash = AdminPasswordHasher.Hash(
        "dummy-password-used-only-for-login-timing");

    private readonly AuthenticationOptions _authenticationOptions = authenticationOptions.Value;
    private readonly AdminAccessOptions _adminOptions = adminOptions.Value;

    public async Task<TokenPairResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        if (!_adminOptions.Enabled)
        {
            throw new AdminAccessUnavailableException();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var credential = await database.AdminCredentials.AsNoTracking().SingleOrDefaultAsync(
            item => item.Email == normalizedEmail,
            cancellationToken);

        var passwordIsValid = AdminPasswordHasher.Verify(
            password,
            credential?.PasswordHash ?? DummyPasswordHash);

        if (credential is null || !passwordIsValid)
        {
            return null;
        }

        var user = await database.Users.SingleOrDefaultAsync(
            item => item.Id == credential.UserId &&
                item.Role == UserRole.Admin &&
                item.IsActive,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var activeSessions = await database.AuthSessions
            .Where(session => session.UserId == user.Id &&
                session.RevokedAt == null &&
                session.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var activeSession in activeSessions)
        {
            activeSession.Revoke(now, "admin_new_login");
        }

        var session = AuthSession.Create(
            user,
            now,
            now.AddDays(_authenticationOptions.RefreshTokenDays));
        var refresh = tokens.CreateRefreshToken(session.Id, now);
        session.AddRefreshToken(SessionRefreshToken.Create(
            refresh.Id,
            session.Id,
            refresh.Hash,
            now,
            refresh.ExpiresAt));
        database.AuthSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);

        var access = tokens.CreateAccessToken(user, session.Id, now);
        return new TokenPairResult(
            access.Value,
            access.ExpiresAt,
            refresh.Value,
            refresh.ExpiresAt,
            new AuthenticatedUser(user.Id, user.PhoneNumber, user.Role));
    }
}
