using Rydo.Application.Maps;
using Rydo.Domain.Pricing;

namespace Rydo.Application.Pricing;

public sealed record FareBreakdownResult(
    decimal DistanceCharge,
    decimal MinimumFareAdjustment,
    decimal BookingFee,
    decimal DemandAdjustment,
    decimal EstimatedTolls,
    decimal WaitingFee,
    decimal Discount);

public sealed record FareOptionResult(
    RideCategory Category,
    decimal RatePerKilometre,
    decimal MinimumFare,
    decimal Total,
    FareBreakdownResult Breakdown);

public sealed record FareQuoteResult(
    Guid Id,
    string PricingVersion,
    string Currency,
    int DistanceMeters,
    int DurationSeconds,
    decimal DemandMultiplier,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<FareOptionResult> Options,
    string EncodedPolyline);

public interface IPricingService
{
    Task<FareQuoteResult> CreateQuoteAsync(
        Guid passengerUserId,
        GeoCoordinate pickup,
        GeoCoordinate destination,
        CancellationToken cancellationToken);
}

public sealed class FareRouteNotFoundException : Exception;
public sealed class PricingValidationException(string message) : Exception(message);
