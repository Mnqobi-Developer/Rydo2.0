using Microsoft.EntityFrameworkCore;
using Rydo.Application.Trips;
using Rydo.Application.Realtime;
using Rydo.Domain.Identity;
using Rydo.Domain.Pricing;
using Rydo.Domain.Trips;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Trips;

public sealed class TripService(
    RydoDbContext database,
    TimeProvider timeProvider,
    IRealtimeEventPublisher realtime) : ITripService
{
    public async Task<TripResult> RequestAsync(
        Guid passengerUserId,
        string pickupAddress,
        double pickupLatitude,
        double pickupLongitude,
        string destinationAddress,
        double destinationLatitude,
        double destinationLongitude,
        Guid fareQuoteId,
        RideCategory rideCategory,
        CancellationToken cancellationToken)
    {
        if (pickupLatitude == destinationLatitude && pickupLongitude == destinationLongitude)
        {
            throw new TripValidationException("Pickup and destination must be different locations.");
        }

        var passengerExists = await database.Users.AnyAsync(
            user => user.Id == passengerUserId &&
                user.IsActive &&
                user.Role == UserRole.Passenger,
            cancellationToken);

        if (!passengerExists)
        {
            throw new TripAccessException("Only an active passenger can request a trip.");
        }

        if (!await database.PassengerProfiles.AnyAsync(
            profile => profile.UserId == passengerUserId,
            cancellationToken))
        {
            throw new PassengerProfileRequiredException();
        }

        if (await database.Trips.AnyAsync(
            trip => trip.PassengerUserId == passengerUserId &&
                trip.Status != TripStatus.Completed &&
                trip.Status != TripStatus.Cancelled,
            cancellationToken))
        {
            throw new ActiveTripConflictException(
                "A passenger cannot request another trip while one is active.");
        }

        var now = timeProvider.GetUtcNow();
        var quote = await database.FareQuotes
            .Include(item => item.Options)
            .SingleOrDefaultAsync(item => item.Id == fareQuoteId, cancellationToken)
            ?? throw new FareQuoteConflictException("The fare quote does not exist. Request a new quote.");

        FareQuoteOption selectedFare;
        try
        {
            selectedFare = quote.Select(passengerUserId, rideCategory, now);
        }
        catch (UnauthorizedAccessException)
        {
            throw new TripAccessException("This fare quote belongs to another passenger.");
        }
        catch (InvalidOperationException exception)
        {
            throw new FareQuoteConflictException(exception.Message);
        }

        if (!CoordinatesMatch(quote, pickupLatitude, pickupLongitude,
                destinationLatitude, destinationLongitude))
        {
            throw new FareQuoteConflictException(
                "Pickup or destination changed after this quote was created. Request a new quote.");
        }

        Trip trip;

        try
        {
            trip = Trip.Request(
                passengerUserId,
                pickupAddress,
                pickupLatitude,
                pickupLongitude,
                destinationAddress,
                destinationLatitude,
                destinationLongitude,
                quote.Id,
                rideCategory,
                selectedFare.Total,
                quote.Currency,
                quote.PricingVersion,
                now);
        }
        catch (ArgumentException exception)
        {
            throw new TripValidationException(exception.Message);
        }

        quote.MarkUsed(now);
        database.Trips.Add(trip);
        await SaveChangesAsync(cancellationToken);
        var result = ToResult(trip);
        await realtime.PublishTripUpdatedAsync(result, cancellationToken);
        return result;
    }

    public async Task<TripResult?> GetAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var query = database.Trips.Where(trip => trip.Id == tripId);
        query = role switch
        {
            UserRole.Passenger => query.Where(trip => trip.PassengerUserId == userId),
            UserRole.Driver => query.Where(trip => trip.DriverUserId == userId),
            _ => query.Where(_ => false),
        };

        return await Project(query).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TripResult>> ListAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var query = role switch
        {
            UserRole.Passenger => database.Trips.Where(
                trip => trip.PassengerUserId == userId),
            UserRole.Driver => database.Trips.Where(
                trip => trip.DriverUserId == userId),
            _ => database.Trips.Where(_ => false),
        };

        return await Project(query.OrderByDescending(trip => trip.RequestedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<TripResult> MarkDriverArrivedAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            tripId,
            trip => trip.MarkDriverArrived(driverUserId, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task<TripResult> StartAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            tripId,
            trip => trip.Start(driverUserId, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task<TripResult> CompleteAsync(
        Guid tripId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            tripId,
            trip => trip.Complete(driverUserId, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task<TripResult> CancelAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        string? reason,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            tripId,
            trip => trip.Cancel(userId, role, reason, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private async Task<TripResult> TransitionAsync(
        Guid tripId,
        Action<Trip> transition,
        CancellationToken cancellationToken)
    {
        var trip = await database.Trips.SingleOrDefaultAsync(
            item => item.Id == tripId,
            cancellationToken) ?? throw new TripNotFoundException();

        try
        {
            transition(trip);
            await SaveChangesAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new TripAccessException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new TripStateConflictException(exception.Message);
        }

        var result = ToResult(trip);
        await realtime.PublishTripUpdatedAsync(result, cancellationToken);
        return result;
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new TripStateConflictException(
                "The trip changed before this operation completed. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            throw new ActiveTripConflictException(
                "The trip conflicts with another active trip.");
        }
    }

    private static IQueryable<TripResult> Project(IQueryable<Trip> query)
    {
        return query.Select(trip => new TripResult(
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
            trip.Version));
    }

    private static TripResult ToResult(Trip trip)
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

    private static bool CoordinatesMatch(
        FareQuote quote,
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude) =>
        Close(quote.PickupLatitude, pickupLatitude)
        && Close(quote.PickupLongitude, pickupLongitude)
        && Close(quote.DestinationLatitude, destinationLatitude)
        && Close(quote.DestinationLongitude, destinationLongitude);

    private static bool Close(double left, double right) => Math.Abs(left - right) < 0.000001;
}
