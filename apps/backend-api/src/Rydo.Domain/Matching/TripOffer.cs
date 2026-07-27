namespace Rydo.Domain.Matching;

public sealed class TripOffer
{
    private TripOffer()
    {
    }

    private TripOffer(
        Guid id,
        Guid tripId,
        Guid driverUserId,
        double pickupDistanceKilometres,
        DateTimeOffset offeredAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TripId = tripId;
        DriverUserId = driverUserId;
        PickupDistanceKilometres = pickupDistanceKilometres;
        Status = TripOfferStatus.Pending;
        OfferedAt = offeredAt;
        ExpiresAt = expiresAt;
        UpdatedAt = offeredAt;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid TripId { get; private set; }

    public Guid DriverUserId { get; private set; }

    public double PickupDistanceKilometres { get; private set; }

    public TripOfferStatus Status { get; private set; }

    public DateTimeOffset OfferedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public int Version { get; private set; }

    public static TripOffer Create(
        Guid tripId,
        Guid driverUserId,
        double pickupDistanceKilometres,
        DateTimeOffset offeredAt,
        DateTimeOffset expiresAt)
    {
        if (expiresAt <= offeredAt)
        {
            throw new ArgumentException("A trip offer must expire after it is created.");
        }

        return new TripOffer(
            Guid.NewGuid(),
            tripId,
            driverUserId,
            Math.Round(pickupDistanceKilometres, 3),
            offeredAt,
            expiresAt);
    }

    public void Accept(DateTimeOffset respondedAt)
    {
        RequirePending(respondedAt);
        Status = TripOfferStatus.Accepted;
        RespondedAt = respondedAt;
        Touch(respondedAt);
    }

    public void Decline(DateTimeOffset respondedAt)
    {
        RequirePending(respondedAt);
        Status = TripOfferStatus.Declined;
        RespondedAt = respondedAt;
        Touch(respondedAt);
    }

    public void Expire(DateTimeOffset updatedAt)
    {
        if (Status != TripOfferStatus.Pending)
        {
            return;
        }

        Status = TripOfferStatus.Expired;
        Touch(updatedAt);
    }

    private void RequirePending(DateTimeOffset respondedAt)
    {
        if (Status != TripOfferStatus.Pending)
        {
            throw new InvalidOperationException(
                $"A trip offer in the {Status} state cannot be answered.");
        }

        if (respondedAt >= ExpiresAt)
        {
            throw new InvalidOperationException("The trip offer has expired.");
        }
    }

    private void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
        Version++;
    }
}
