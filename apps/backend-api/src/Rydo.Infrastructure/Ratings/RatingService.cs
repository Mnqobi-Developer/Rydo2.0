using Microsoft.EntityFrameworkCore;
using Rydo.Application.Ratings;
using Rydo.Domain.Identity;
using Rydo.Domain.Ratings;
using Rydo.Domain.Trips;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Ratings;

public sealed class RatingService(RydoDbContext database, TimeProvider timeProvider) : IRatingService
{
    public async Task<RatingResult> CreateAsync(Guid tripId, Guid raterUserId, UserRole raterRole, int score, string? comment, CancellationToken cancellationToken)
    {
        var trip = await database.Trips.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tripId, cancellationToken)
            ?? throw new RatingNotFoundException();
        var ratedUserId = ResolveRatedUser(trip, raterUserId, raterRole);

        if (trip.Status != TripStatus.Completed)
        {
            throw new RatingStateConflictException("A trip can only be rated after completion.");
        }

        Rating candidate;
        try
        {
            candidate = Rating.Create(trip.Id, raterUserId, ratedUserId, score, comment, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            throw new RatingValidationException(exception.Message);
        }

        var existing = await FindAsync(tripId, raterUserId, cancellationToken);
        if (existing is not null) return ResolveDuplicate(existing, candidate);

        database.Ratings.Add(candidate);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.Entry(candidate).State = EntityState.Detached;
            existing = await FindAsync(tripId, raterUserId, cancellationToken);
            if (existing is not null) return ResolveDuplicate(existing, candidate);
            throw;
        }

        return ToResult(candidate);
    }

    public async Task<RatingResult?> GetOwnForTripAsync(Guid tripId, Guid raterUserId, UserRole raterRole, CancellationToken cancellationToken)
    {
        var trip = await database.Trips.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tripId, cancellationToken)
            ?? throw new RatingNotFoundException();
        _ = ResolveRatedUser(trip, raterUserId, raterRole);
        var rating = await FindAsync(tripId, raterUserId, cancellationToken);
        return rating is null ? null : ToResult(rating);
    }

    public async Task<RatingSummaryResult> GetOwnSummaryAsync(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role is not UserRole.Passenger and not UserRole.Driver || !await IsActiveUserAsync(userId, role, cancellationToken))
        {
            throw new RatingAccessException("Only an active Passenger or Driver can view ratings.");
        }

        return await BuildSummaryAsync(userId, cancellationToken);
    }

    public async Task<RatingSummaryResult> GetDriverSummaryAsync(Guid driverUserId, CancellationToken cancellationToken)
    {
        if (!await IsActiveUserAsync(driverUserId, UserRole.Driver, cancellationToken))
        {
            throw new RatedUserNotFoundException();
        }

        return await BuildSummaryAsync(driverUserId, cancellationToken);
    }

    private Task<Rating?> FindAsync(Guid tripId, Guid raterUserId, CancellationToken cancellationToken) =>
        database.Ratings.AsNoTracking().SingleOrDefaultAsync(rating => rating.TripId == tripId && rating.RaterUserId == raterUserId, cancellationToken);

    private Task<bool> IsActiveUserAsync(Guid userId, UserRole role, CancellationToken cancellationToken) =>
        database.Users.AnyAsync(user => user.Id == userId && user.Role == role && user.IsActive, cancellationToken);

    private async Task<RatingSummaryResult> BuildSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var scores = await database.Ratings.Where(rating => rating.RatedUserId == userId).Select(rating => rating.Score).ToListAsync(cancellationToken);
        var distribution = Enumerable.Range(1, 5).ToDictionary(score => score, score => scores.Count(item => item == score));
        var average = scores.Count == 0 ? (double?)null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero);
        return new RatingSummaryResult(userId, average, scores.Count, distribution);
    }

    private static Guid ResolveRatedUser(Trip trip, Guid raterUserId, UserRole role)
    {
        if (role == UserRole.Passenger && trip.PassengerUserId == raterUserId && trip.DriverUserId is Guid driverUserId) return driverUserId;
        if (role == UserRole.Driver && trip.DriverUserId == raterUserId) return trip.PassengerUserId;
        throw new RatingAccessException("Only the Passenger and assigned Driver can rate this trip.");
    }

    private static RatingResult ResolveDuplicate(Rating existing, Rating candidate)
    {
        if (existing.RatedUserId == candidate.RatedUserId && existing.Score == candidate.Score && existing.Comment == candidate.Comment) return ToResult(existing);
        throw new RatingStateConflictException("This participant has already rated the trip.");
    }

    private static RatingResult ToResult(Rating rating) =>
        new(rating.Id, rating.TripId, rating.RaterUserId, rating.RatedUserId, rating.Score, rating.Comment, rating.CreatedAt);
}
