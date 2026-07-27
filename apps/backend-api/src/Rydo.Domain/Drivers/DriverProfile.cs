namespace Rydo.Domain.Drivers;

public sealed class DriverProfile
{
    private DriverProfile()
    {
    }

    private DriverProfile(
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
        OnboardingStatus = DriverOnboardingStatus.Draft;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public DriverOnboardingStatus OnboardingStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public bool CanEdit => OnboardingStatus is
        DriverOnboardingStatus.Draft or DriverOnboardingStatus.Rejected;

    public static DriverProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        string? email,
        DateTimeOffset createdAt)
    {
        return new DriverProfile(
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
        if (!CanEdit)
        {
            throw new InvalidOperationException(
                "A driver profile cannot be edited while it is under review or approved.");
        }

        FirstName = NormalizeName(firstName);
        LastName = NormalizeName(lastName);
        Email = NormalizeEmail(email);
        OnboardingStatus = DriverOnboardingStatus.Draft;
        SubmittedAt = null;
        ReviewedAt = null;
        RejectionReason = null;
        UpdatedAt = updatedAt;
    }

    public void Submit(DateTimeOffset submittedAt)
    {
        if (OnboardingStatus != DriverOnboardingStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft driver profile can be submitted for review.");
        }

        OnboardingStatus = DriverOnboardingStatus.PendingReview;
        SubmittedAt = submittedAt;
        UpdatedAt = submittedAt;
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
