using System.ComponentModel.DataAnnotations;
using Rydo.Domain.Pricing;

namespace Rydo.Infrastructure.Pricing;

public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    [Required, MaxLength(64)]
    public string Version { get; init; } = "za-launch-2026-07-29";

    [Required, RegularExpression("^[A-Z]{3}$")]
    public string Currency { get; init; } = "ZAR";

    [Range(1, 30)]
    public int QuoteLifetimeMinutes { get; init; } = 5;

    public decimal DemandMultiplier { get; init; } = 1m;

    public decimal BookingFee { get; init; }

    public Dictionary<RideCategory, CategoryPricingOptions> Categories { get; init; } = new()
    {
        [RideCategory.Solo] = new() { RatePerKilometre = 8.50m, MinimumFare = 25m },
        [RideCategory.Group] = new() { RatePerKilometre = 13m, MinimumFare = 35m },
        [RideCategory.GroupPlus] = new() { RatePerKilometre = 18m, MinimumFare = 50m },
    };

    public bool IsValid() =>
        DemandMultiplier is >= 1m and <= 1.5m
        && BookingFee is >= 0m and <= 1000m
        && Enum.GetValues<RideCategory>().All(category =>
            Categories.TryGetValue(category, out var rule) && rule.IsValid());
}

public sealed class CategoryPricingOptions
{
    public decimal RatePerKilometre { get; init; }
    public decimal MinimumFare { get; init; }

    public bool IsValid() => RatePerKilometre > 0 && MinimumFare > 0;
}
