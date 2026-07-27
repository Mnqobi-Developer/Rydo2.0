namespace Rydo.Domain.Passengers;

public sealed class PassengerProfile
{
    private PassengerProfile()
    {
    }

    private PassengerProfile(
        Guid userId,
        string firstName,
        string lastName,
        string? email,
        DateTimeOffset createdAt)
    {
        UserId = userId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static PassengerProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        string? email,
        DateTimeOffset createdAt)
    {
        return new PassengerProfile(
            userId,
            NormalizeName(firstName),
            NormalizeName(lastName),
            NormalizeEmail(email),
            createdAt);
    }

    public void Update(
        string firstName,
        string lastName,
        string? email,
        DateTimeOffset updatedAt)
    {
        FirstName = NormalizeName(firstName);
        LastName = NormalizeName(lastName);
        Email = NormalizeEmail(email);
        UpdatedAt = updatedAt;
    }

    private static string NormalizeName(string value)
    {
        return value.Trim();
    }

    private static string? NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}
