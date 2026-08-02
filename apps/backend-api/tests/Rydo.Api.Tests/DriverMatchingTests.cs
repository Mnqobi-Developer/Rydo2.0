using System.Net;
using System.Net.Http.Json;
using Rydo.Application.Matching;
using Rydo.Domain.Matching;
using Rydo.Domain.Trips;

namespace Rydo.Api.Tests;

public sealed class DriverMatchingTests
{
    [Fact]
    public async Task DriverPerformanceUpdatesWhenTripIsCompleted()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001200");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001299",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            driver,
            1299);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        await TripTestClient.TransitionAsync(client, trip.Id, "arrive");
        await TripTestClient.TransitionAsync(client, trip.Id, "start");
        await TripTestClient.TransitionAsync(client, trip.Id, "complete");

        var response = await client.GetAsync("/api/v1/drivers/me/performance");
        response.EnsureSuccessStatusCode();
        var performance = await response.Content.ReadFromJsonAsync<DriverPerformanceResult>();

        Assert.NotNull(performance);
        Assert.Equal(100, performance.AcceptanceRate);
        Assert.Equal(100, performance.CompletionRate);
        Assert.Null(performance.AverageRating);
        Assert.Equal(0, performance.RatingCount);
    }

    [Fact]
    public async Task UnapprovedDriverCannotGoOnline()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001201",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/availability/online",
            new { latitude = -33.925, longitude = 18.424 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EligibleDriverCanManageAvailabilityAndLocation()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001202",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            driver,
            1202);

        var getResponse = await client.GetAsync("/api/v1/drivers/me/availability");
        getResponse.EnsureSuccessStatusCode();
        var online = await DriverMatchingTestClient.ReadAvailabilityAsync(getResponse);
        Assert.True(online.IsOnline);

        factory.Clock.Advance(TimeSpan.FromSeconds(15));
        var locationResponse = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/location",
            new { latitude = -33.921, longitude = 18.429 });
        locationResponse.EnsureSuccessStatusCode();
        var moved = await DriverMatchingTestClient.ReadAvailabilityAsync(locationResponse);
        Assert.Equal(-33.921, moved.Latitude);
        Assert.Equal(18.429, moved.Longitude);
        Assert.True(moved.Version > online.Version);

        var offlineResponse = await client.PostAsync(
            "/api/v1/drivers/me/availability/offline",
            null);
        offlineResponse.EnsureSuccessStatusCode();
        var offline = await DriverMatchingTestClient.ReadAvailabilityAsync(offlineResponse);
        Assert.False(offline.IsOnline);

        var updateWhileOffline = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/location",
            new { latitude = -33.92, longitude = 18.43 });
        Assert.Equal(HttpStatusCode.Conflict, updateWhileOffline.StatusCode);
    }

    [Fact]
    public async Task MatchingOffersTripOnlyToNearbyEligibleDrivers()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001203");
        var trip = await TripTestClient.RequestAsync(client);
        var nearbyDriver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001204",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            nearbyDriver,
            1204,
            -33.925,
            18.424);
        var distantDriver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001205",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            distantDriver,
            1205,
            -34.2,
            18.424);

        var result = await DriverMatchingTestClient.MatchAsync(
            client,
            passenger.AccessToken,
            trip.Id);

        Assert.Equal(1, result.OfferedDriverCount);
        Assert.NotNull(result.OffersExpireAt);

        AuthenticationTestClient.UseBearerToken(client, nearbyDriver.AccessToken);
        var nearbyOffersResponse = await client.GetAsync("/api/v1/drivers/me/trip-offers");
        nearbyOffersResponse.EnsureSuccessStatusCode();
        var nearbyOffers = await DriverMatchingTestClient.ReadOffersAsync(nearbyOffersResponse);
        var offer = Assert.Single(nearbyOffers);
        Assert.Equal(trip.Id, offer.TripId);
        Assert.Equal(TripOfferStatus.Pending, offer.Status);
        Assert.True(offer.PickupDistanceKilometres < 1);

        AuthenticationTestClient.UseBearerToken(client, distantDriver.AccessToken);
        var distantOffersResponse = await client.GetAsync("/api/v1/drivers/me/trip-offers");
        distantOffersResponse.EnsureSuccessStatusCode();
        Assert.Empty(await DriverMatchingTestClient.ReadOffersAsync(distantOffersResponse));
    }

    [Fact]
    public async Task DriverCanDeclineAnOwnedOffer()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001206");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001207",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            driver,
            1207);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);

        var declineResponse = await client.PostAsync(
            $"/api/v1/drivers/me/trip-offers/{trip.Id}/decline",
            null);
        declineResponse.EnsureSuccessStatusCode();
        var offersResponse = await client.GetAsync("/api/v1/drivers/me/trip-offers");
        offersResponse.EnsureSuccessStatusCode();

        Assert.Empty(await DriverMatchingTestClient.ReadOffersAsync(offersResponse));
    }

    [Fact]
    public async Task ExpiredOfferCannotBeAccepted()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001208");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001209",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            driver,
            1209);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);

        var response = await client.PostAsync($"/api/v1/trips/{trip.Id}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AcceptingOfferAssignsDriverAndExpiresCompetingOffers()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001210");
        var trip = await TripTestClient.RequestAsync(client);
        var firstDriver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001211",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            firstDriver,
            1211,
            -33.925,
            18.424);
        var secondDriver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001212",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            secondDriver,
            1212,
            -33.926,
            18.425);
        var match = await DriverMatchingTestClient.MatchAsync(
            client,
            passenger.AccessToken,
            trip.Id);
        Assert.Equal(2, match.OfferedDriverCount);

        AuthenticationTestClient.UseBearerToken(client, firstDriver.AccessToken);
        var accepted = await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        Assert.Equal(firstDriver.User.Id, accepted.DriverUserId);
        Assert.Equal(TripStatus.Accepted, accepted.Status);

        var availabilityResponse = await client.GetAsync("/api/v1/drivers/me/availability");
        availabilityResponse.EnsureSuccessStatusCode();
        Assert.False((await DriverMatchingTestClient.ReadAvailabilityAsync(
            availabilityResponse)).IsOnline);

        AuthenticationTestClient.UseBearerToken(client, secondDriver.AccessToken);
        var competingOffersResponse = await client.GetAsync("/api/v1/drivers/me/trip-offers");
        competingOffersResponse.EnsureSuccessStatusCode();
        Assert.Empty(await DriverMatchingTestClient.ReadOffersAsync(competingOffersResponse));
    }

    [Fact]
    public async Task PassengerCannotMatchAnotherPassengersTrip()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001213");
        var trip = await TripTestClient.RequestAsync(client);
        await TripTestClient.CreatePassengerAsync(
            client,
            "+27820001214");

        var response = await client.PostAsync($"/api/v1/trips/{trip.Id}/matching", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
