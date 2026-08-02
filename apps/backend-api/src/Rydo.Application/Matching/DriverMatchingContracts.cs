using Rydo.Application.Trips;
using Rydo.Domain.Matching;
using Rydo.Domain.Pricing;

namespace Rydo.Application.Matching;

public sealed record DriverAvailabilityResult(
    Guid DriverUserId,
    bool IsOnline,
    double Latitude,
    double Longitude,
    DateTimeOffset? LocationUpdatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record TripOfferResult(
    Guid Id,
    Guid TripId,
    Guid DriverUserId,
    string PickupAddress,
    double PickupLatitude,
    double PickupLongitude,
    string DestinationAddress,
    double DestinationLatitude,
    double DestinationLongitude,
    double PickupDistanceKilometres,
    RideCategory? RideCategory,
    decimal? EstimatedFareAmount,
    string? FareCurrency,
    TripOfferStatus Status,
    DateTimeOffset OfferedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    int Version);

public sealed record TripMatchingResult(
    Guid TripId,
    int OfferedDriverCount,
    DateTimeOffset? OffersExpireAt);

public sealed record DriverPerformanceResult(
    double? AcceptanceRate,
    double? CompletionRate,
    double? AverageRating,
    int RatingCount);

public interface IDriverMatchingService
{
    Task<DriverAvailabilityResult?> GetAvailabilityAsync(
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<DriverAvailabilityResult> GoOnlineAsync(
        Guid driverUserId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken);

    Task<DriverAvailabilityResult?> GoOfflineAsync(
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<DriverAvailabilityResult> UpdateLocationAsync(
        Guid driverUserId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken);

    Task<TripMatchingResult> MatchAsync(
        Guid tripId,
        Guid passengerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripOfferResult>> ListOffersAsync(
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<DriverPerformanceResult> GetPerformanceAsync(
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<TripOfferResult> DeclineOfferAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task<TripResult> AcceptOfferAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken);
}

public sealed class DriverNotEligibleException : Exception
{
    public DriverNotEligibleException()
        : base("Driver onboarding must be approved before going online or accepting trips.")
    {
    }
}

public sealed class DriverAvailabilityNotFoundException : Exception;

public sealed class DriverAvailabilityConflictException(string message) : Exception(message);

public sealed class TripMatchingAccessException : Exception;

public sealed class TripMatchingStateException(string message) : Exception(message);

public sealed class TripOfferNotFoundException : Exception;
