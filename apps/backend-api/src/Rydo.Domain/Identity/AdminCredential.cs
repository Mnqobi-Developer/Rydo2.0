namespace Rydo.Domain.Identity;

public sealed class AdminCredential
{
    private AdminCredential()
    {
    }

    private AdminCredential(
        Guid userId,
        string email,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        UserId = userId;
        Email = NormalizeEmail(email);
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        PasswordUpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset PasswordUpdatedAt { get; private set; }

    public static AdminCredential Create(
        Guid userId,
        string email,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Admin credential values are required.");
        }

        return new AdminCredential(userId, email, passwordHash, createdAt);
    }

    public void RotatePassword(string passwordHash, DateTimeOffset updatedAt)
    {
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash)
            ? throw new ArgumentException("A password hash is required.")
            : passwordHash;
        PasswordUpdatedAt = updatedAt;
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return normalized.Length is > 3 and <= 254 && normalized.Contains('@')
            ? normalized
            : throw new ArgumentException("A valid admin email address is required.");
    }
}
