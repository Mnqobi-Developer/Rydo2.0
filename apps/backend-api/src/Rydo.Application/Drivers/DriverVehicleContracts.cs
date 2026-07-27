using Rydo.Domain.Drivers;

namespace Rydo.Application.Drivers;

public sealed record DriverVehicleResult(
    Guid Id,
    Guid DriverUserId,
    string Make,
    string Model,
    int Year,
    string Color,
    string RegistrationNumber,
    string VehicleIdentificationNumber,
    int SeatCapacity,
    DriverVehicleReviewStatus ReviewStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

public interface IDriverVehicleService
{
    Task<DriverVehicleResult?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<DriverVehicleResult?> UpsertAsync(
        Guid userId,
        string make,
        string model,
        int year,
        string color,
        string registrationNumber,
        string vehicleIdentificationNumber,
        int seatCapacity,
        CancellationToken cancellationToken);
}

public sealed class DriverVehicleConflictException(string message) : Exception(message);

public sealed class DriverVehicleValidationException(string message) : Exception(message);

public sealed class DriverOnboardingVehicleMissingException : Exception
{
    public DriverOnboardingVehicleMissingException()
        : base("Add a current vehicle before submitting onboarding for review.")
    {
    }
}
