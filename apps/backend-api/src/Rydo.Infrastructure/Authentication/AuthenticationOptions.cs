using System.ComponentModel.DataAnnotations;

namespace Rydo.Infrastructure.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(64)]
    public string SigningKey { get; init; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string OtpPepper { get; init; } = string.Empty;

    [Range(5, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 30;

    [Range(1, 15)]
    public int OtpLifetimeMinutes { get; init; } = 5;

    [Range(3, 10)]
    public int OtpMaximumAttempts { get; init; } = 5;
}
