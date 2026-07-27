using Microsoft.EntityFrameworkCore;
using Rydo.Application.Drivers;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Drivers;

public sealed class DriverVehicleService(
    RydoDbContext database,
    TimeProvider timeProvider) : IDriverVehicleService
{
    public async Task<DriverVehicleResult?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        return await database.DriverVehicles
            .Where(vehicle => vehicle.DriverUserId == userId)
            .Select(vehicle => new DriverVehicleResult(
                vehicle.Id,
                vehicle.DriverUserId,
                vehicle.Make,
                vehicle.Model,
                vehicle.Year,
                vehicle.Color,
                vehicle.RegistrationNumber,
                vehicle.VehicleIdentificationNumber,
                vehicle.SeatCapacity,
                vehicle.ReviewStatus,
                vehicle.CreatedAt,
                vehicle.UpdatedAt,
                vehicle.ReviewedAt,
                vehicle.RejectionReason))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<DriverVehicleResult?> UpsertAsync(
        Guid userId,
        string make,
        string model,
        int year,
        string color,
        string registrationNumber,
        string vehicleIdentificationNumber,
        int seatCapacity,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        var profile = await database.DriverProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId,
            cancellationToken) ?? throw new DriverProfileNotFoundException();

        if (!profile.CanEdit)
        {
            throw new DriverVehicleConflictException(
                "Vehicle information cannot change while onboarding is under review or approved.");
        }

        var maximumYear = timeProvider.GetUtcNow().Year + 1;

        if (year > maximumYear)
        {
            throw new DriverVehicleValidationException(
                $"Vehicle year cannot be later than {maximumYear}.");
        }

        var normalizedRegistration = registrationNumber.Trim().ToUpperInvariant();
        var normalizedVin = vehicleIdentificationNumber.Trim().ToUpperInvariant();
        var conflicts = await database.DriverVehicles.AnyAsync(
            vehicle => vehicle.DriverUserId != userId &&
                (vehicle.RegistrationNumber == normalizedRegistration ||
                    vehicle.VehicleIdentificationNumber == normalizedVin),
            cancellationToken);

        if (conflicts)
        {
            throw new DriverVehicleConflictException(
                "The registration number or vehicle identification number is already registered.");
        }

        var now = timeProvider.GetUtcNow();
        var vehicle = await database.DriverVehicles
            .SingleOrDefaultAsync(item => item.DriverUserId == userId, cancellationToken);

        if (vehicle is null)
        {
            vehicle = DriverVehicle.Create(
                userId,
                make,
                model,
                year,
                color,
                normalizedRegistration,
                normalizedVin,
                seatCapacity,
                now);
            database.DriverVehicles.Add(vehicle);
        }
        else
        {
            vehicle.Update(
                make,
                model,
                year,
                color,
                normalizedRegistration,
                normalizedVin,
                seatCapacity,
                now);
        }

        await database.SaveChangesAsync(cancellationToken);
        return ToResult(vehicle);
    }

    private Task<bool> IsActiveDriverAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return database.Users.AnyAsync(
            user => user.Id == userId &&
                user.IsActive &&
                user.Role == UserRole.Driver,
            cancellationToken);
    }

    private static DriverVehicleResult ToResult(DriverVehicle vehicle)
    {
        return new DriverVehicleResult(
            vehicle.Id,
            vehicle.DriverUserId,
            vehicle.Make,
            vehicle.Model,
            vehicle.Year,
            vehicle.Color,
            vehicle.RegistrationNumber,
            vehicle.VehicleIdentificationNumber,
            vehicle.SeatCapacity,
            vehicle.ReviewStatus,
            vehicle.CreatedAt,
            vehicle.UpdatedAt,
            vehicle.ReviewedAt,
            vehicle.RejectionReason);
    }
}
