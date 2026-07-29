namespace Rydo.Domain.Pricing;

public sealed class FareQuote
{
    private readonly List<FareQuoteOption> _options = [];

    private FareQuote()
    {
    }

    private FareQuote(
        Guid passengerUserId,
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude,
        int distanceMeters,
        int durationSeconds,
        string pricingVersion,
        string currency,
        decimal demandMultiplier,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        PassengerUserId = passengerUserId;
        PickupLatitude = pickupLatitude;
        PickupLongitude = pickupLongitude;
        DestinationLatitude = destinationLatitude;
        DestinationLongitude = destinationLongitude;
        DistanceMeters = distanceMeters;
        DurationSeconds = durationSeconds;
        PricingVersion = pricingVersion;
        Currency = currency;
        DemandMultiplier = demandMultiplier;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid PassengerUserId { get; private set; }
    public double PickupLatitude { get; private set; }
    public double PickupLongitude { get; private set; }
    public double DestinationLatitude { get; private set; }
    public double DestinationLongitude { get; private set; }
    public int DistanceMeters { get; private set; }
    public int DurationSeconds { get; private set; }
    public string PricingVersion { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public decimal DemandMultiplier { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public IReadOnlyCollection<FareQuoteOption> Options => _options;

    public static FareQuote Create(
        Guid passengerUserId,
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude,
        int distanceMeters,
        int durationSeconds,
        string pricingVersion,
        string currency,
        decimal demandMultiplier,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (distanceMeters <= 0 || durationSeconds <= 0 || expiresAt <= createdAt)
        {
            throw new ArgumentException("A fare quote requires a valid route and expiry.");
        }

        return new FareQuote(
            passengerUserId,
            pickupLatitude,
            pickupLongitude,
            destinationLatitude,
            destinationLongitude,
            distanceMeters,
            durationSeconds,
            pricingVersion.Trim(),
            currency.Trim().ToUpperInvariant(),
            demandMultiplier,
            createdAt,
            expiresAt);
    }

    public void AddOption(FareQuoteOption option)
    {
        if (_options.Any(existing => existing.Category == option.Category))
        {
            throw new InvalidOperationException("A quote cannot contain duplicate ride categories.");
        }

        option.AttachTo(Id);
        _options.Add(option);
    }

    public FareQuoteOption Select(Guid passengerUserId, RideCategory category, DateTimeOffset now)
    {
        if (PassengerUserId != passengerUserId)
        {
            throw new UnauthorizedAccessException("This fare quote belongs to another passenger.");
        }

        if (UsedAt is not null)
        {
            throw new InvalidOperationException("This fare quote has already been used.");
        }

        if (now >= ExpiresAt)
        {
            throw new InvalidOperationException("This fare quote has expired.");
        }

        return _options.SingleOrDefault(option => option.Category == category)
            ?? throw new InvalidOperationException("The selected ride category is not in this quote.");
    }

    public void MarkUsed(DateTimeOffset usedAt)
    {
        if (UsedAt is not null)
        {
            throw new InvalidOperationException("This fare quote has already been used.");
        }

        UsedAt = usedAt;
    }
}
