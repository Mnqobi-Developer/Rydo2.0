namespace Rydo.Domain.Identity;

public sealed class UserAccount
{
    private UserAccount()
    {
    }

    private UserAccount(Guid id, string phoneNumber, UserRole role, DateTimeOffset createdAt)
    {
        Id = id;
        PhoneNumber = phoneNumber;
        Role = role;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string PhoneNumber { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static UserAccount Create(string phoneNumber, UserRole role, DateTimeOffset createdAt)
    {
        return new UserAccount(Guid.NewGuid(), phoneNumber, role, createdAt);
    }
}
