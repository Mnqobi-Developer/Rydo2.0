using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Rydo.Domain.Identity;

namespace Rydo.Infrastructure.Authentication;

public sealed class CryptoTokenService(IOptions<AuthenticationOptions> options)
{
    private readonly AuthenticationOptions _options = options.Value;

    public static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
    }

    public string HashOtp(Guid challengeId, string code)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.OtpPepper));
        var value = Encoding.UTF8.GetBytes($"{challengeId:N}:{code}");
        return Convert.ToHexString(hmac.ComputeHash(value));
    }

    public bool VerifyOtp(Guid challengeId, string code, string expectedHash)
    {
        var actual = Convert.FromHexString(HashOtp(challengeId, code));
        var expected = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public RefreshTokenMaterial CreateRefreshToken(Guid sessionId, DateTimeOffset now)
    {
        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = HashRefreshToken(value);
        var expiresAt = now.AddDays(_options.RefreshTokenDays);

        return new RefreshTokenMaterial(Guid.NewGuid(), sessionId, value, hash, expiresAt);
    }

    public static string HashRefreshToken(string refreshToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    public AccessTokenMaterial CreateAccessToken(
        UserAccount user,
        Guid sessionId,
        DateTimeOffset now)
    {
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(
                CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim("sid", sessionId.ToString()),
            new Claim("phone_number", user.PhoneNumber),
            new Claim("role", user.Role.ToString().ToLowerInvariant()),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new AccessTokenMaterial(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}

public sealed record RefreshTokenMaterial(
    Guid Id,
    Guid SessionId,
    string Value,
    string Hash,
    DateTimeOffset ExpiresAt);

public sealed record AccessTokenMaterial(string Value, DateTimeOffset ExpiresAt);
