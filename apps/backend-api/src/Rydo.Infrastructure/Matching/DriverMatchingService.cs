using Microsoft.EntityFrameworkCore;
using Rydo.Application.Matching;
using Rydo.Application.Realtime;
using Rydo.Application.Trips;
using Rydo.Domain.Drivers;
using Rydo.Domain.Matching;
using Rydo.Domain.Trips;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Matching;

public sealed class DriverMatchingService(
    RydoDbContext database,
    TimeProvider timeProvider,
    IRealtimeEventPublisher realtime) : IDriverMatchingService
{
    private const int MaximumOffersPerWave = 5;
    private const double MaximumPickupDistanceKilometres = 20;
    private static readonly TimeSpan LocationFreshness = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OfferLifetime = TimeSpan.FromSeconds(30);

    public Task<DriverAvailabilityResult?> GetAvailabilityAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        return ProjectAvailability(database.DriverAvailability
            .Where(item => item.DriverUserId == driverUserId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<DriverAvailabilityResult> GoOnlineAsync(
        Guid driverUserId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        await RequireEligibleDriverAsync(driverUserId, cancellationToken);

        if (await HasActiveTripAsync(driverUserId, cancellationToken))
        {
            throw new DriverAvailabilityConflictException(
                "A driver with an active trip cannot change availability.");
        }

        var now = timeProvider.GetUtcNow();
        var availability = await database.DriverAvailability.SingleOrDefaultAsync(
            item => item.DriverUserId == driverUserId,
            cancellationToken);

        try
        {
            if (availability is null)
            {
                availability = DriverAvailability.GoOnline(
                    driverUserId,
                    latitude,
                    longitude,
                    now);
                database.DriverAvailability.Add(availability);
            }
            else
            {
                availability.SetOnline(latitude, longitude, now);
            }
        }
        catch (ArgumentException exception)
        {
            throw new DriverAvailabilityConflictException(exception.Message);
        }

        await SaveChangesAsync(cancellationToken);
        var result = ToAvailabilityResult(availability);
        await realtime.PublishDriverAvailabilityUpdatedAsync(
            result,
            null,
            cancellationToken);
        return result;
    }

    public async Task<DriverAvailabilityResult?> GoOfflineAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        if (await HasActiveTripAsync(driverUserId, cancellationToken))
        {
            throw new DriverAvailabilityConflictException(
                "A driver with an active trip cannot go offline.");
        }

        var availability = await database.DriverAvailability.SingleOrDefaultAsync(
            item => item.DriverUserId == driverUserId,
            cancellationToken);

        if (availability is null)
        {
            return null;
        }

        availability.SetOffline(timeProvider.GetUtcNow());
        await SaveChangesAsync(cancellationToken);
        var result = ToAvailabilityResult(availability);
        await realtime.PublishDriverAvailabilityUpdatedAsync(
            result,
            null,
            cancellationToken);
        return result;
    }

    public async Task<DriverAvailabilityResult> UpdateLocationAsync(
        Guid driverUserId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var availability = await database.DriverAvailability.SingleOrDefaultAsync(
            item => item.DriverUserId == driverUserId,
            cancellationToken) ?? throw new DriverAvailabilityNotFoundException();
        var activePassengerUserId = await database.Trips
            .Where(trip => trip.DriverUserId == driverUserId &&
                trip.Status != TripStatus.Completed &&
                trip.Status != TripStatus.Cancelled)
            .Select(trip => (Guid?)trip.PassengerUserId)
            .SingleOrDefaultAsync(cancellationToken);

        try
        {
            var now = timeProvider.GetUtcNow();

            if (availability.IsOnline)
            {
                availability.UpdateLocation(latitude, longitude, now);
            }
            else if (activePassengerUserId is not null)
            {
                availability.UpdateAssignedTripLocation(latitude, longitude, now);
            }
            else
            {
                throw new InvalidOperationException(
                    "A driver must be online or assigned to an active trip before updating location.");
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            throw new DriverAvailabilityConflictException(exception.Message);
        }

        await SaveChangesAsync(cancellationToken);
        var result = ToAvailabilityResult(availability);
        await realtime.PublishDriverAvailabilityUpdatedAsync(
            result,
            activePassengerUserId,
            cancellationToken);
        return result;
    }

    public async Task<TripMatchingResult> MatchAsync(
        Guid tripId,
        Guid passengerUserId,
        CancellationToken cancellationToken)
    {
        var trip = await database.Trips.SingleOrDefaultAsync(
            item => item.Id == tripId,
            cancellationToken) ?? throw new TripNotFoundException();

        if (trip.PassengerUserId != passengerUserId)
        {
            throw new TripMatchingAccessException();
        }

        if (trip.Status != TripStatus.Requested)
        {
            throw new TripMatchingStateException(
                "Only a requested trip can search for drivers.");
        }

        var now = timeProvider.GetUtcNow();
        var existingOffers = await database.TripOffers
            .Where(offer => offer.TripId == tripId)
            .ToListAsync(cancellationToken);

        foreach (var staleOffer in existingOffers.Where(
            offer => offer.Status == TripOfferStatus.Pending && offer.ExpiresAt <= now))
        {
            staleOffer.Expire(now);
        }

        var pendingOffers = existingOffers
            .Where(offer => offer.Status == TripOfferStatus.Pending && offer.ExpiresAt > now)
            .ToList();

        if (pendingOffers.Count > 0)
        {
            await SaveChangesAsync(cancellationToken);
            return new TripMatchingResult(
                tripId,
                pendingOffers.Count,
                pendingOffers.Min(offer => offer.ExpiresAt));
        }

        var locationCutoff = now - LocationFreshness;
        var previouslyOfferedDriverIds = existingOffers
            .Select(offer => offer.DriverUserId)
            .ToHashSet();
        var activeDriverIds = await database.Trips
            .Where(item => item.DriverUserId != null &&
                item.Status != TripStatus.Completed &&
                item.Status != TripStatus.Cancelled)
            .Select(item => item.DriverUserId!.Value)
            .ToListAsync(cancellationToken);
        var eligibleDrivers = await (
            from availability in database.DriverAvailability
            join profile in database.DriverProfiles
                on availability.DriverUserId equals profile.UserId
            where availability.IsOnline &&
                availability.LocationUpdatedAt >= locationCutoff &&
                profile.OnboardingStatus == DriverOnboardingStatus.Approved
            select availability)
            .ToListAsync(cancellationToken);

        var candidates = eligibleDrivers
            .Where(driver => !previouslyOfferedDriverIds.Contains(driver.DriverUserId) &&
                !activeDriverIds.Contains(driver.DriverUserId))
            .Select(driver => new
            {
                Driver = driver,
                Distance = HaversineKilometres(
                    trip.PickupLatitude,
                    trip.PickupLongitude,
                    driver.Latitude,
                    driver.Longitude),
            })
            .Where(candidate => candidate.Distance <= MaximumPickupDistanceKilometres)
            .OrderBy(candidate => candidate.Distance)
            .ThenByDescending(candidate => candidate.Driver.LocationUpdatedAt)
            .Take(MaximumOffersPerWave)
            .ToList();
        var expiresAt = now + OfferLifetime;

        var createdOffers = new List<TripOffer>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var createdOffer = TripOffer.Create(
                tripId,
                candidate.Driver.DriverUserId,
                candidate.Distance,
                now,
                expiresAt);
            createdOffers.Add(createdOffer);
            database.TripOffers.Add(createdOffer);
        }

        await SaveChangesAsync(cancellationToken);
        var createdOfferIds = createdOffers.Select(offer => offer.Id).ToArray();
        var offerResults = await ProjectOffers(database.TripOffers.Where(
                offer => createdOfferIds.Contains(offer.Id)))
            .ToListAsync(cancellationToken);

        foreach (var offerResult in offerResults)
        {
            await realtime.PublishTripOfferUpdatedAsync(offerResult, cancellationToken);
        }

        return new TripMatchingResult(
            tripId,
            candidates.Count,
            candidates.Count == 0 ? null : expiresAt);
    }

    public async Task<IReadOnlyList<TripOfferResult>> ListOffersAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var staleOffers = await database.TripOffers
            .Where(offer => offer.DriverUserId == driverUserId &&
                offer.Status == TripOfferStatus.Pending &&
                offer.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var offer in staleOffers)
        {
            offer.Expire(now);
        }

        await SaveChangesAsync(cancellationToken);

        var pendingOffers = database.TripOffers
            .Where(offer => offer.DriverUserId == driverUserId &&
                offer.Status == TripOfferStatus.Pending &&
                offer.ExpiresAt > now)
            .OrderBy(offer => offer.PickupDistanceKilometres);

        return await ProjectOffers(pendingOffers)
            .ToListAsync(cancellationToken);
    }

    public async Task<DriverPerformanceResult> GetPerformanceAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var resolvedOffers = database.TripOffers
            .Where(offer => offer.DriverUserId == driverUserId &&
                offer.Status != TripOfferStatus.Pending);
        var resolvedOfferCount = await resolvedOffers.CountAsync(cancellationToken);
        var acceptedOfferCount = await resolvedOffers.CountAsync(
            offer => offer.Status == TripOfferStatus.Accepted,
            cancellationToken);
        var completedTripCount = await database.Trips.CountAsync(
            trip => trip.DriverUserId == driverUserId && trip.Status == TripStatus.Completed,
            cancellationToken);
        var driverCancelledTripCount = await database.Trips.CountAsync(
            trip => trip.DriverUserId == driverUserId &&
                trip.Status == TripStatus.Cancelled &&
                trip.CancelledByUserId == driverUserId,
            cancellationToken);
        var ratingScores = await database.Ratings
            .Where(rating => rating.RatedUserId == driverUserId)
            .Select(rating => rating.Score)
            .ToListAsync(cancellationToken);
        var terminalTripCount = completedTripCount + driverCancelledTripCount;

        return new DriverPerformanceResult(
            Percentage(acceptedOfferCount, resolvedOfferCount),
            Percentage(completedTripCount, terminalTripCount),
            ratingScores.Count == 0
                ? null
                : Math.Round(ratingScores.Average(), 2, MidpointRounding.AwayFromZero),
            ratingScores.Count);
    }

    public async Task<TripOfferResult> DeclineOfferAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var offer = await FindOfferAsync(tripId, driverUserId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (offer.Status == TripOfferStatus.Pending && offer.ExpiresAt <= now)
        {
            offer.Expire(now);
            await SaveChangesAsync(cancellationToken);
            throw new TripMatchingStateException("The trip offer has expired.");
        }

        try
        {
            offer.Decline(now);
        }
        catch (InvalidOperationException exception)
        {
            throw new TripMatchingStateException(exception.Message);
        }

        await SaveChangesAsync(cancellationToken);
        var result = await ProjectOffers(database.TripOffers.Where(item => item.Id == offer.Id))
            .SingleAsync(cancellationToken);
        await realtime.PublishTripOfferUpdatedAsync(result, cancellationToken);
        return result;
    }

    public async Task<TripResult> AcceptOfferAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        await RequireEligibleDriverAsync(driverUserId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var availability = await database.DriverAvailability.SingleOrDefaultAsync(
            item => item.DriverUserId == driverUserId,
            cancellationToken) ?? throw new DriverAvailabilityNotFoundException();

        if (!availability.IsOnline || availability.LocationUpdatedAt < now - LocationFreshness)
        {
            throw new DriverAvailabilityConflictException(
                "The driver must be online with a recent location to accept a trip.");
        }

        if (await HasActiveTripAsync(driverUserId, cancellationToken))
        {
            throw new DriverAvailabilityConflictException(
                "A driver cannot accept another trip while one is active.");
        }

        var offer = await FindOfferAsync(tripId, driverUserId, cancellationToken);

        if (offer.Status == TripOfferStatus.Pending && offer.ExpiresAt <= now)
        {
            offer.Expire(now);
            await SaveChangesAsync(cancellationToken);
            throw new TripMatchingStateException("The trip offer has expired.");
        }

        var trip = await database.Trips.SingleOrDefaultAsync(
            item => item.Id == tripId,
            cancellationToken) ?? throw new TripNotFoundException();

        try
        {
            offer.Accept(now);
            trip.Accept(driverUserId, now);
        }
        catch (InvalidOperationException exception)
        {
            throw new TripMatchingStateException(exception.Message);
        }

        var competingOffers = await database.TripOffers
            .Where(item => item.TripId == tripId &&
                item.Id != offer.Id &&
                item.Status == TripOfferStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var competingOffer in competingOffers)
        {
            competingOffer.Expire(now);
        }

        availability.SetOffline(now);
        await SaveChangesAsync(cancellationToken);
        var changedOfferIds = competingOffers.Select(item => item.Id)
            .Append(offer.Id)
            .ToArray();
        var changedOffers = await ProjectOffers(database.TripOffers.Where(
                item => changedOfferIds.Contains(item.Id)))
            .ToListAsync(cancellationToken);

        foreach (var changedOffer in changedOffers)
        {
            await realtime.PublishTripOfferUpdatedAsync(changedOffer, cancellationToken);
        }

        var tripResult = ToTripResult(trip);
        await realtime.PublishTripUpdatedAsync(tripResult, cancellationToken);
        await realtime.PublishDriverAvailabilityUpdatedAsync(
            ToAvailabilityResult(availability),
            trip.PassengerUserId,
            cancellationToken);
        return tripResult;
    }

    private async Task RequireEligibleDriverAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        if (!await database.DriverProfiles.AnyAsync(
            profile => profile.UserId == driverUserId &&
                profile.OnboardingStatus == DriverOnboardingStatus.Approved,
            cancellationToken))
        {
            throw new DriverNotEligibleException();
        }
    }

    private Task<bool> HasActiveTripAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        return database.Trips.AnyAsync(
            trip => trip.DriverUserId == driverUserId &&
                trip.Status != TripStatus.Completed &&
                trip.Status != TripStatus.Cancelled,
            cancellationToken);
    }

    private async Task<TripOffer> FindOfferAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        return await database.TripOffers.SingleOrDefaultAsync(
            offer => offer.TripId == tripId && offer.DriverUserId == driverUserId,
            cancellationToken) ?? throw new TripOfferNotFoundException();
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new TripMatchingStateException(
                "Matching state changed before the operation completed. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            throw new TripMatchingStateException(
                "Matching state conflicts with another active operation.");
        }
    }

    private static IQueryable<DriverAvailabilityResult> ProjectAvailability(
        IQueryable<DriverAvailability> query)
    {
        return query.Select(item => new DriverAvailabilityResult(
            item.DriverUserId,
            item.IsOnline,
            item.Latitude,
            item.Longitude,
            item.LocationUpdatedAt,
            item.UpdatedAt,
            item.Version));
    }

    private IQueryable<TripOfferResult> ProjectOffers(IQueryable<TripOffer> query)
    {
        return from offer in query
               join trip in database.Trips on offer.TripId equals trip.Id
               select new TripOfferResult(
                   offer.Id,
                   offer.TripId,
                   offer.DriverUserId,
                   trip.PickupAddress,
                   trip.PickupLatitude,
                   trip.PickupLongitude,
                   trip.DestinationAddress,
                   trip.DestinationLatitude,
                   trip.DestinationLongitude,
                   offer.PickupDistanceKilometres,
                   trip.RideCategory,
                   trip.EstimatedFareAmount,
                   trip.FareCurrency,
                   offer.Status,
                   offer.OfferedAt,
                   offer.ExpiresAt,
                   offer.RespondedAt,
                   offer.Version);
    }

    private static DriverAvailabilityResult ToAvailabilityResult(
        DriverAvailability availability)
    {
        return new DriverAvailabilityResult(
            availability.DriverUserId,
            availability.IsOnline,
            availability.Latitude,
            availability.Longitude,
            availability.LocationUpdatedAt,
            availability.UpdatedAt,
            availability.Version);
    }

    private static TripResult ToTripResult(Trip trip)
    {
        return new TripResult(
            trip.Id,
            trip.PassengerUserId,
            trip.DriverUserId,
            trip.PickupAddress,
            trip.PickupLatitude,
            trip.PickupLongitude,
            trip.DestinationAddress,
            trip.DestinationLatitude,
            trip.DestinationLongitude,
            trip.FareQuoteId,
            trip.RideCategory,
            trip.EstimatedFareAmount,
            trip.FareCurrency,
            trip.PricingVersion,
            trip.Status,
            trip.RequestedAt,
            trip.UpdatedAt,
            trip.AcceptedAt,
            trip.DriverArrivedAt,
            trip.StartedAt,
            trip.CompletedAt,
            trip.CancelledAt,
            trip.CancelledByUserId,
            trip.CancellationReason,
            trip.FinalFareAmount,
            trip.Version);
    }

    private static double HaversineKilometres(
        double firstLatitude,
        double firstLongitude,
        double secondLatitude,
        double secondLongitude)
    {
        const double earthRadiusKilometres = 6371.0088;
        var latitudeDelta = DegreesToRadians(secondLatitude - firstLatitude);
        var longitudeDelta = DegreesToRadians(secondLongitude - firstLongitude);
        var firstLatitudeRadians = DegreesToRadians(firstLatitude);
        var secondLatitudeRadians = DegreesToRadians(secondLatitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            Math.Cos(firstLatitudeRadians) * Math.Cos(secondLatitudeRadians) *
            Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return 2 * earthRadiusKilometres * Math.Asin(Math.Sqrt(haversine));
    }

    private static double? Percentage(int numerator, int denominator) =>
        denominator == 0
            ? null
            : Math.Round(numerator * 100d / denominator, 1, MidpointRounding.AwayFromZero);

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
