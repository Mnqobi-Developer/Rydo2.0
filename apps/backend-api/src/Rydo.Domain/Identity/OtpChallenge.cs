namespace Rydo.Domain.Identity;

public sealed class OtpChallenge
{
    private OtpChallenge()
    {
    }

    private OtpChallenge(
        Guid id,
        string phoneNumber,
        UserRole requestedRole,
        string codeHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        int maximumAttempts)
    {
        Id = id;
        PhoneNumber = phoneNumber;
        RequestedRole = requestedRole;
        CodeHash = codeHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        MaximumAttempts = maximumAttempts;
    }

    public Guid Id { get; private set; }

    public string PhoneNumber { get; private set; } = string.Empty;

    public UserRole RequestedRole { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public int MaximumAttempts { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool CanVerify(DateTimeOffset now)
    {
        return ConsumedAt is null && ExpiresAt > now && FailedAttempts < MaximumAttempts;
    }

    public void RegisterFailedAttempt()
    {
        FailedAttempts++;
    }

    public void Consume(DateTimeOffset now)
    {
        ConsumedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        ConsumedAt ??= now;
    }

    public static OtpChallenge Create(
        Guid id,
        string phoneNumber,
        UserRole requestedRole,
        string codeHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        int maximumAttempts)
    {
        return new OtpChallenge(
            id,
            phoneNumber,
            requestedRole,
            codeHash,
            createdAt,
            expiresAt,
            maximumAttempts);
    }
}
