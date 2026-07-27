using Rydo.Domain.Identity;

namespace Rydo.Application.Ratings;

public sealed record RatingResult(Guid Id, Guid TripId, Guid RaterUserId, Guid RatedUserId, int Score, string? Comment, DateTimeOffset CreatedAt);

public sealed record RatingSummaryResult(Guid UserId, double? AverageScore, int RatingCount, IReadOnlyDictionary<int, int> Distribution);

public interface IRatingService
{
    Task<RatingResult> CreateAsync(Guid tripId, Guid raterUserId, UserRole raterRole, int score, string? comment, CancellationToken cancellationToken);
    Task<RatingResult?> GetOwnForTripAsync(Guid tripId, Guid raterUserId, UserRole raterRole, CancellationToken cancellationToken);
    Task<RatingSummaryResult> GetOwnSummaryAsync(Guid userId, UserRole role, CancellationToken cancellationToken);
    Task<RatingSummaryResult> GetDriverSummaryAsync(Guid driverUserId, CancellationToken cancellationToken);
}

public sealed class RatingNotFoundException : Exception;
public sealed class RatedUserNotFoundException : Exception;
public sealed class RatingAccessException(string message) : Exception(message);
public sealed class RatingStateConflictException(string message) : Exception(message);
public sealed class RatingValidationException(string message) : Exception(message);
