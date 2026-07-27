using Rydo.Domain.Identity;
using Rydo.Domain.Trips;

namespace Rydo.Application.Trips;

public sealed record TripResult(
    Guid Id,
    Guid PassengerUserId,
    Guid? DriverUserId,
    string PickupAddress,
    double PickupLatitude,
    double PickupLongitude,
    string DestinationAddress,
    double DestinationLatitude,
    double DestinationLongitude,
    TripStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? DriverArrivedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    Guid? CancelledByUserId,
    string? CancellationReason,
    int Version);

public interface ITripService
{
    Task<TripResult> RequestAsync(
        Guid passengerUserId,
        string pickupAddress,
        double pickupLatitude,
        double pickupLongitude,
        string destinationAddress,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken);

    Task<TripResult?> GetAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripResult>> ListAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken);

    Task<TripResult> AcceptAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<TripResult> MarkDriverArrivedAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<TripResult> StartAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<TripResult> CompleteAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<TripResult> CancelAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        string? reason,
        CancellationToken cancellationToken);
}

public sealed class TripNotFoundException : Exception;

public sealed class TripAccessException(string message) : Exception(message);

public sealed class TripStateConflictException(string message) : Exception(message);

public sealed class TripValidationException(string message) : Exception(message);

public sealed class ActiveTripConflictException(string message) : Exception(message);

public sealed class PassengerProfileRequiredException : Exception
{
    public PassengerProfileRequiredException()
        : base("Create a passenger profile before requesting a trip.")
    {
    }
}
