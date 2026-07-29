namespace Rydo.Domain.Pricing;

public sealed class FareQuoteOption
{
    private FareQuoteOption()
    {
    }

    public FareQuoteOption(
        RideCategory category,
        decimal ratePerKilometre,
        decimal minimumFare,
        decimal distanceCharge,
        decimal minimumFareAdjustment,
        decimal bookingFee,
        decimal demandAdjustment,
        decimal estimatedTolls,
        decimal waitingFee,
        decimal discount,
        decimal total)
    {
        Category = category;
        RatePerKilometre = ratePerKilometre;
        MinimumFare = minimumFare;
        DistanceCharge = distanceCharge;
        MinimumFareAdjustment = minimumFareAdjustment;
        BookingFee = bookingFee;
        DemandAdjustment = demandAdjustment;
        EstimatedTolls = estimatedTolls;
        WaitingFee = waitingFee;
        Discount = discount;
        Total = total;
    }

    public Guid FareQuoteId { get; private set; }
    public RideCategory Category { get; private set; }
    public decimal RatePerKilometre { get; private set; }
    public decimal MinimumFare { get; private set; }
    public decimal DistanceCharge { get; private set; }
    public decimal MinimumFareAdjustment { get; private set; }
    public decimal BookingFee { get; private set; }
    public decimal DemandAdjustment { get; private set; }
    public decimal EstimatedTolls { get; private set; }
    public decimal WaitingFee { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Total { get; private set; }

    internal void AttachTo(Guid fareQuoteId) => FareQuoteId = fareQuoteId;
}
