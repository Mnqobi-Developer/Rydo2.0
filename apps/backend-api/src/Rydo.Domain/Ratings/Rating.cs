namespace Rydo.Domain.Ratings;

public sealed class Rating
{
    private Rating() { }

    private Rating(Guid id, Guid tripId, Guid raterUserId, Guid ratedUserId, int score, string? comment, DateTimeOffset createdAt)
    {
        Id = id;
        TripId = tripId;
        RaterUserId = raterUserId;
        RatedUserId = ratedUserId;
        Score = ValidateScore(score);
        Comment = NormalizeComment(comment);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TripId { get; private set; }
    public Guid RaterUserId { get; private set; }
    public Guid RatedUserId { get; private set; }
    public int Score { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Rating Create(Guid tripId, Guid raterUserId, Guid ratedUserId, int score, string? comment, DateTimeOffset createdAt)
    {
        if (tripId == Guid.Empty || raterUserId == Guid.Empty || ratedUserId == Guid.Empty)
        {
            throw new ArgumentException("Rating identifiers are required.");
        }

        if (raterUserId == ratedUserId)
        {
            throw new ArgumentException("A user cannot rate themselves.");
        }

        return new Rating(Guid.NewGuid(), tripId, raterUserId, ratedUserId, score, comment, createdAt);
    }

    private static int ValidateScore(int score)
    {
        if (score is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Ratings must be between 1 and 5.");
        }

        return score;
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return null;
        var normalized = comment.Trim();
        if (normalized.Length > 500)
        {
            throw new ArgumentException("Rating comments cannot exceed 500 characters.");
        }

        return normalized;
    }
}
