using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Pricing;
using Rydo.Application.Trips;
using Rydo.Domain.Pricing;

namespace Rydo.Api.Tests;

public sealed class PricingTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task QuoteUsesConfiguredRatesAndMinimumFares()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820002001");

        var response = await client.PostAsJsonAsync(
            "/api/v1/pricing/quotes", TripTestClient.ValidQuoteRequest());
        response.EnsureSuccessStatusCode();
        var quote = (await response.Content.ReadFromJsonAsync<FareQuoteResult>(JsonOptions))!;

        Assert.Equal("ZAR", quote.Currency);
        Assert.Equal("za-launch-2026-07-29", quote.PricingVersion);
        Assert.Equal(2_000, quote.DistanceMeters);
        Assert.Equal(1m, quote.DemandMultiplier);
        Assert.Collection(quote.Options,
            solo => AssertOption(solo, RideCategory.Solo, 8.50m, 25m, 17m, 8m, 25m),
            group => AssertOption(group, RideCategory.Group, 13m, 35m, 26m, 9m, 35m),
            groupPlus => AssertOption(groupPlus, RideCategory.GroupPlus, 18m, 50m, 36m, 14m, 50m));
    }

    [Fact]
    public async Task TripConsumesTheSelectedImmutableQuoteOption()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820002002");
        var quote = await CreateQuoteAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/trips", TripTestClient.ValidRequest(quote.Id, RideCategory.Group));
        response.EnsureSuccessStatusCode();
        var trip = (await response.Content.ReadFromJsonAsync<TripResult>(JsonOptions))!;

        Assert.Equal(quote.Id, trip.FareQuoteId);
        Assert.Equal(RideCategory.Group, trip.RideCategory);
        Assert.Equal(35m, trip.EstimatedFareAmount);
        Assert.Equal("ZAR", trip.FareCurrency);
        Assert.Equal(quote.PricingVersion, trip.PricingVersion);
    }

    [Fact]
    public async Task ExpiredQuoteCannotCreateATrip()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820002003");
        var quote = await CreateQuoteAsync(client);
        factory.Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await client.PostAsJsonAsync(
            "/api/v1/trips", TripTestClient.ValidRequest(quote.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task QuoteCannotBeUsedByAnotherPassenger()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820002004");
        var quote = await CreateQuoteAsync(client);
        await TripTestClient.CreatePassengerAsync(client, "+27820002005");

        var response = await client.PostAsJsonAsync(
            "/api/v1/trips", TripTestClient.ValidRequest(quote.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangedCoordinatesRequireANewQuote()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820002006");
        var quote = await CreateQuoteAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/trips", new
        {
            pickupAddress = "Changed pickup",
            pickupLatitude = -33.9259,
            pickupLongitude = 18.4241,
            destinationAddress = "V&A Waterfront, Cape Town",
            destinationLatitude = -33.9036,
            destinationLongitude = 18.4209,
            fareQuoteId = quote.Id,
            rideCategory = RideCategory.Solo,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static async Task<FareQuoteResult> CreateQuoteAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/pricing/quotes", TripTestClient.ValidQuoteRequest());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FareQuoteResult>(JsonOptions))!;
    }

    private static void AssertOption(
        FareOptionResult option,
        RideCategory category,
        decimal rate,
        decimal minimum,
        decimal distanceCharge,
        decimal minimumAdjustment,
        decimal total)
    {
        Assert.Equal(category, option.Category);
        Assert.Equal(rate, option.RatePerKilometre);
        Assert.Equal(minimum, option.MinimumFare);
        Assert.Equal(distanceCharge, option.Breakdown.DistanceCharge);
        Assert.Equal(minimumAdjustment, option.Breakdown.MinimumFareAdjustment);
        Assert.Equal(total, option.Total);
        Assert.Equal(0m, option.Breakdown.BookingFee);
        Assert.Equal(0m, option.Breakdown.DemandAdjustment);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
