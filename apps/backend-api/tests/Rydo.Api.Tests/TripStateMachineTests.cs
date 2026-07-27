using System.Net;
using System.Net.Http.Json;
using Rydo.Application.Trips;
using Rydo.Domain.Trips;

namespace Rydo.Api.Tests;

public sealed class TripStateMachineTests
{
    [Fact]
    public async Task PassengerAndDriverCanCompleteTheTripLifecycle()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001001");
        var requested = await TripTestClient.RequestAsync(client);

        Assert.Equal(passenger.User.Id, requested.PassengerUserId);
        Assert.Null(requested.DriverUserId);
        Assert.Equal(TripStatus.Requested, requested.Status);
        Assert.Equal(1, requested.Version);

        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001002",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var accepted = await TripTestClient.TransitionAsync(client, requested.Id, "accept");
        Assert.Equal(driver.User.Id, accepted.DriverUserId);
        Assert.Equal(TripStatus.Accepted, accepted.Status);
        Assert.NotNull(accepted.AcceptedAt);
        Assert.Equal(2, accepted.Version);

        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var arrived = await TripTestClient.TransitionAsync(client, requested.Id, "arrive");
        Assert.Equal(TripStatus.DriverArrived, arrived.Status);
        Assert.NotNull(arrived.DriverArrivedAt);
        Assert.Equal(3, arrived.Version);

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var started = await TripTestClient.TransitionAsync(client, requested.Id, "start");
        Assert.Equal(TripStatus.InProgress, started.Status);
        Assert.NotNull(started.StartedAt);
        Assert.Equal(4, started.Version);

        factory.Clock.Advance(TimeSpan.FromMinutes(15));
        var completed = await TripTestClient.TransitionAsync(client, requested.Id, "complete");
        Assert.Equal(TripStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal(5, completed.Version);
        Assert.True(completed.UpdatedAt > completed.RequestedAt);

        AuthenticationTestClient.UseBearerToken(client, passenger.AccessToken);
        var getResponse = await client.GetAsync($"/api/v1/trips/{requested.Id}");
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal(completed, await TripTestClient.ReadAsync(getResponse));
    }

    [Fact]
    public async Task StateTransitionsMustOccurInOrder()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001003");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001004",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);

        var startBeforeAccept = await client.PostAsync(
            $"/api/v1/trips/{trip.Id}/start",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, startBeforeAccept.StatusCode);

        await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        var completeBeforeStart = await client.PostAsync(
            $"/api/v1/trips/{trip.Id}/complete",
            null);
        Assert.Equal(HttpStatusCode.Conflict, completeBeforeStart.StatusCode);
    }

    [Fact]
    public async Task OnlyAssignedDriverCanControlAcceptedTrip()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001005");
        var trip = await TripTestClient.RequestAsync(client);
        var assignedDriver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001006",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, assignedDriver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");

        var otherDriver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001007",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, otherDriver.AccessToken);
        var response = await client.PostAsync($"/api/v1/trips/{trip.Id}/arrive", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PassengerCanCancelTripBeforeItStarts()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001008");
        var trip = await TripTestClient.RequestAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/cancel",
            new { reason = " Plans changed " });
        response.EnsureSuccessStatusCode();
        var cancelled = await TripTestClient.ReadAsync(response);

        Assert.Equal(TripStatus.Cancelled, cancelled.Status);
        Assert.Equal(passenger.User.Id, cancelled.CancelledByUserId);
        Assert.Equal("Plans changed", cancelled.CancellationReason);
        Assert.NotNull(cancelled.CancelledAt);

        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/cancel",
            new { reason = "again" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task InProgressTripCannotBeCancelled()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001009");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001010",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        await TripTestClient.TransitionAsync(client, trip.Id, "arrive");
        await TripTestClient.TransitionAsync(client, trip.Id, "start");
        AuthenticationTestClient.UseBearerToken(client, passenger.AccessToken);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/cancel",
            new { reason = "too late" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PassengerCannotHaveTwoActiveTrips()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001011");
        await TripTestClient.RequestAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/trips", TripTestClient.ValidRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DriverCannotAcceptTwoActiveTrips()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001012");
        var firstTrip = await TripTestClient.RequestAsync(client);
        await TripTestClient.CreatePassengerAsync(client, "+27820001013");
        var secondTrip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001014",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, firstTrip.Id, "accept");

        var response = await client.PostAsync($"/api/v1/trips/{secondTrip.Id}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TripHistoryIsVisibleOnlyToParticipants()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001015");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001016",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");

        var driverListResponse = await client.GetAsync("/api/v1/trips/me");
        driverListResponse.EnsureSuccessStatusCode();
        var driverList = await TripTestClient.ReadListAsync(driverListResponse);
        Assert.Single(driverList);

        await TripTestClient.CreatePassengerAsync(
            client,
            "+27820001017");
        var hiddenResponse = await client.GetAsync($"/api/v1/trips/{trip.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);

        AuthenticationTestClient.UseBearerToken(client, passenger.AccessToken);
        var passengerListResponse = await client.GetAsync("/api/v1/trips/me");
        passengerListResponse.EnsureSuccessStatusCode();
        var passengerList = await TripTestClient.ReadListAsync(passengerListResponse);
        Assert.Single(passengerList);
        Assert.Equal(trip.Id, passengerList[0].Id);
    }

    [Fact]
    public async Task TripRequestRequiresPassengerProfile()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001018",
            "Passenger");
        AuthenticationTestClient.UseBearerToken(client, passenger.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/trips", TripTestClient.ValidRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(91, 18.4, -33.9, 18.5)]
    [InlineData(-33.9, 181, -33.8, 18.5)]
    [InlineData(-33.9, 18.4, -33.9, 18.4)]
    public async Task InvalidLocationsAreRejected(
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(
            client,
            $"+2782000110{Math.Abs((int)pickupLatitude) % 10}");

        var response = await client.PostAsJsonAsync(
            "/api/v1/trips",
            new
            {
                pickupAddress = "Pickup",
                pickupLatitude,
                pickupLongitude,
                destinationAddress = "Destination",
                destinationLatitude,
                destinationLongitude,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
