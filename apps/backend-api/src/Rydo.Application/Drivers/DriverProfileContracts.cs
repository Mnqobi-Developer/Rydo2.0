using Rydo.Domain.Drivers;

namespace Rydo.Application.Drivers;

public sealed record DriverProfileResult(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    DriverOnboardingStatus OnboardingStatus,
    bool CanEdit,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

public interface IDriverProfileService
{
    Task<DriverProfileResult?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<DriverProfileResult?> UpsertAsync(
        Guid userId,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken);

    Task<DriverProfileResult?> SubmitAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public sealed class DriverProfileNotFoundException : Exception
{
    public DriverProfileNotFoundException()
        : base("Create a driver profile before submitting onboarding for review.")
    {
    }
}

public sealed class DriverOnboardingStateException(string message) : Exception(message);
