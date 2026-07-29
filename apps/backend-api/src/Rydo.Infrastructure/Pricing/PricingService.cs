using Microsoft.Extensions.Options;
using Rydo.Application.Maps;
using Rydo.Application.Pricing;
using Rydo.Domain.Pricing;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Pricing;

public sealed class PricingService(
    RydoDbContext database,
    IMapService maps,
    IOptions<PricingOptions> options,
    TimeProvider timeProvider) : IPricingService
{
    private readonly PricingOptions _options = options.Value;

    public async Task<FareQuoteResult> CreateQuoteAsync(
        Guid passengerUserId,
        GeoCoordinate pickup,
        GeoCoordinate destination,
        CancellationToken cancellationToken)
    {
        Validate(pickup, destination);
        var route = await maps.ComputeRouteAsync(
            new RouteRequest(pickup, destination), cancellationToken)
            ?? throw new FareRouteNotFoundException();
        var now = timeProvider.GetUtcNow();
        var quote = FareQuote.Create(
            passengerUserId,
            pickup.Latitude,
            pickup.Longitude,
            destination.Latitude,
            destination.Longitude,
            route.DistanceMeters,
            route.DurationSeconds,
            _options.Version,
            _options.Currency,
            _options.DemandMultiplier,
            now,
            now.AddMinutes(_options.QuoteLifetimeMinutes));

        foreach (var category in Enum.GetValues<RideCategory>())
        {
            var rule = _options.Categories[category];
            quote.AddOption(Calculate(category, rule, route.DistanceMeters));
        }

        database.FareQuotes.Add(quote);
        await database.SaveChangesAsync(cancellationToken);
        return ToResult(quote, route.EncodedPolyline);
    }

    private FareQuoteOption Calculate(
        RideCategory category,
        CategoryPricingOptions rule,
        int distanceMeters)
    {
        var kilometres = distanceMeters / 1000m;
        var distanceCharge = Money(kilometres * rule.RatePerKilometre);
        var minimumAdjustment = Money(Math.Max(0, rule.MinimumFare - distanceCharge));
        var subtotal = distanceCharge + minimumAdjustment + _options.BookingFee;
        var demandAdjustment = Money(subtotal * (_options.DemandMultiplier - 1m));
        var total = Money(subtotal + demandAdjustment);
        return new FareQuoteOption(
            category,
            rule.RatePerKilometre,
            rule.MinimumFare,
            distanceCharge,
            minimumAdjustment,
            _options.BookingFee,
            demandAdjustment,
            estimatedTolls: 0,
            waitingFee: 0,
            discount: 0,
            total);
    }

    private static FareQuoteResult ToResult(FareQuote quote, string encodedPolyline) => new(
        quote.Id,
        quote.PricingVersion,
        quote.Currency,
        quote.DistanceMeters,
        quote.DurationSeconds,
        quote.DemandMultiplier,
        quote.CreatedAt,
        quote.ExpiresAt,
        quote.Options.OrderBy(option => option.Category).Select(option => new FareOptionResult(
            option.Category,
            option.RatePerKilometre,
            option.MinimumFare,
            option.Total,
            new FareBreakdownResult(
                option.DistanceCharge,
                option.MinimumFareAdjustment,
                option.BookingFee,
                option.DemandAdjustment,
                option.EstimatedTolls,
                option.WaitingFee,
                option.Discount))).ToArray(),
        encodedPolyline);

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void Validate(GeoCoordinate pickup, GeoCoordinate destination)
    {
        if (!Valid(pickup) || !Valid(destination))
        {
            throw new PricingValidationException("Pickup and destination must contain valid coordinates.");
        }

        if (pickup == destination)
        {
            throw new PricingValidationException("Pickup and destination must be different locations.");
        }
    }

    private static bool Valid(GeoCoordinate coordinate) =>
        double.IsFinite(coordinate.Latitude) && coordinate.Latitude is >= -90 and <= 90
        && double.IsFinite(coordinate.Longitude) && coordinate.Longitude is >= -180 and <= 180;
}
