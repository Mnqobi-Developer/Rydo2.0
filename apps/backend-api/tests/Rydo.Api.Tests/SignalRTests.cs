using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rydo.Application.Realtime;
using Rydo.Domain.Matching;
using Rydo.Domain.Trips;

namespace Rydo.Api.Tests;

public sealed class SignalRTests
{
    [Fact]
    public async Task OperationsHubRequiresAnAuthenticatedSession()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var anonymous = await client.PostAsync(
            "/hubs/operations/negotiate?negotiateVersion=1",
            null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var passenger = await TripTestClient.CreatePassengerAsync(
            client,
            "+27821210001");
        AuthenticationTestClient.UseBearerToken(client, passenger.AccessToken);
        var authenticated = await client.PostAsync(
            "/hubs/operations/negotiate?negotiateVersion=1",
            null);

        authenticated.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TripMatchingAndAssignedLocationPublishRealtimeEvents()
    {
        var events = new FakeRealtimeEventPublisher();
        await using var factory = CreateFactory(events);
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(
            client,
            "+27821210002");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27821210003",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            driver,
            5103);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");

        var locationResponse = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/location",
            new { latitude = -33.921, longitude = 18.429 });

        locationResponse.EnsureSuccessStatusCode();
        Assert.Contains(events.Trips, item =>
            item.Id == trip.Id && item.Status == TripStatus.Requested);
        Assert.Contains(events.Trips, item =>
            item.Id == trip.Id && item.Status == TripStatus.Accepted);
        Assert.Contains(events.Offers, item =>
            item.TripId == trip.Id && item.Status == TripOfferStatus.Pending);
        Assert.Contains(events.Offers, item =>
            item.TripId == trip.Id && item.Status == TripOfferStatus.Accepted);
        Assert.Contains(events.Availabilities, item =>
            item.Availability.DriverUserId == driver.User.Id &&
            item.PassengerUserId == passenger.User.Id &&
            item.Availability.Latitude == -33.921);
    }

    [Fact]
    public async Task PaymentsAndDisputesPublishRealtimeEvents()
    {
        var events = new FakeRealtimeEventPublisher();
        await using var factory = CreateFactory(events);
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(
            factory,
            client,
            "211",
            5211);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var payment = await PaymentTestClient.CreateAsync(client, scenario.Trip.Id, "Cash");
        var disputeResponse = await DisputeTestClient.OpenAsync(client, scenario.Trip.Id);
        disputeResponse.EnsureSuccessStatusCode();
        var dispute = await DisputeTestClient.ReadAsync(disputeResponse);

        Assert.Contains(events.Payments, item => item.Id == payment.Payment.Id);
        Assert.Contains(events.Disputes, item => item.Id == dispute.Id);
    }

    private static AuthenticationApiFactory CreateFactory(
        FakeRealtimeEventPublisher events)
    {
        return new AuthenticationApiFactory(services =>
        {
            services.RemoveAll<IRealtimeEventPublisher>();
            services.AddSingleton<IRealtimeEventPublisher>(events);
        });
    }
}
