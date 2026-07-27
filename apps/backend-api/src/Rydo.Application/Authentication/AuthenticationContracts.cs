using Rydo.Domain.Identity;

namespace Rydo.Application.Authentication;

public sealed record OtpRequestResult(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    string? DevelopmentCode);

public sealed record AuthenticatedUser(Guid Id, string PhoneNumber, UserRole Role);

public sealed record TokenPairResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUser User);

public interface IAuthenticationService
{
    Task<OtpRequestResult> RequestOtpAsync(
        string phoneNumber,
        UserRole role,
        CancellationToken cancellationToken);

    Task<TokenPairResult?> VerifyOtpAsync(
        Guid challengeId,
        string code,
        CancellationToken cancellationToken);

    Task<TokenPairResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<AuthenticatedUser?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public sealed class AuthenticationConflictException(string message) : Exception(message);

public sealed class AuthenticationRateLimitException(string message) : Exception(message);
