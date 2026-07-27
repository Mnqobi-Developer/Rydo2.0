using System.Net;
using System.Net.Http.Json;

namespace Rydo.Api.Tests;

public sealed class RatingTests
{
    [Fact]
    public async Task PassengerAndDriverCanRateEachOtherAfterCompletion()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "001", 3001);

        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var passengerResponse = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 5, "  Great driver  ");
        Assert.Equal(HttpStatusCode.Created, passengerResponse.StatusCode);
        var passengerRating = await RatingTestClient.ReadAsync(passengerResponse);
        Assert.Equal(scenario.Passenger.User.Id, passengerRating.RaterUserId);
        Assert.Equal(scenario.Driver.User.Id, passengerRating.RatedUserId);
        Assert.Equal("Great driver", passengerRating.Comment);

        AuthenticationTestClient.UseBearerToken(client, scenario.Driver.AccessToken);
        var driverResponse = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 4, "Ready on time");
        Assert.Equal(HttpStatusCode.Created, driverResponse.StatusCode);
        var driverRating = await RatingTestClient.ReadAsync(driverResponse);
        Assert.Equal(scenario.Driver.User.Id, driverRating.RaterUserId);
        Assert.Equal(scenario.Passenger.User.Id, driverRating.RatedUserId);
    }

    [Fact]
    public async Task TripCannotBeRatedBeforeCompletion()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27821002001");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(client, "+27821002002", "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(factory, client, driver, 3002);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        AuthenticationTestClient.UseBearerToken(client, passenger.AccessToken);

        var response = await RatingTestClient.CreateAsync(client, trip.Id, 5, null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task NonParticipantCannotReadOrRateTrip()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "003", 3003);
        var outsider = await TripTestClient.CreatePassengerAsync(client, "+27821003003");
        AuthenticationTestClient.UseBearerToken(client, outsider.AccessToken);

        var createResponse = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 3, null);
        var readResponse = await client.GetAsync($"/api/v1/trips/{scenario.Trip.Id}/ratings/me");

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readResponse.StatusCode);
    }

    [Fact]
    public async Task IdenticalRetryIsIdempotentButReplacementConflicts()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "004", 3004);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);

        var firstResponse = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 5, "Excellent");
        var first = await RatingTestClient.ReadAsync(firstResponse);
        var retryResponse = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 5, "  Excellent ");
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(first, await RatingTestClient.ReadAsync(retryResponse));

        var replacementResponse = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 4, "Excellent");
        Assert.Equal(HttpStatusCode.Conflict, replacementResponse.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task ScoreMustBeBetweenOneAndFive(int score)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, $"05{score}", 3050 + score);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);

        var response = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, score, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CommentCannotExceedFiveHundredCharacters()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "006", 3006);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);

        var response = await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 5, new string('x', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OwnRatingAndAggregateSummariesAreAvailableWithoutExposingComments()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "007", 3007);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 4, "Private feedback");

        var ownResponse = await client.GetAsync($"/api/v1/trips/{scenario.Trip.Id}/ratings/me");
        ownResponse.EnsureSuccessStatusCode();
        Assert.Equal("Private feedback", (await RatingTestClient.ReadAsync(ownResponse)).Comment);

        var driverSummaryResponse = await client.GetAsync($"/api/v1/drivers/{scenario.Driver.User.Id}/ratings/summary");
        driverSummaryResponse.EnsureSuccessStatusCode();
        var driverSummaryJson = await driverSummaryResponse.Content.ReadAsStringAsync();
        var driverSummary = await RatingTestClient.ReadSummaryAsync(driverSummaryResponse);
        Assert.Equal(4, driverSummary.AverageScore);
        Assert.Equal(1, driverSummary.RatingCount);
        Assert.Equal(1, driverSummary.Distribution[4]);
        Assert.DoesNotContain("comment", driverSummaryJson, StringComparison.OrdinalIgnoreCase);

        AuthenticationTestClient.UseBearerToken(client, scenario.Driver.AccessToken);
        await RatingTestClient.CreateAsync(client, scenario.Trip.Id, 5, "Also private");
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var ownSummaryResponse = await client.GetAsync("/api/v1/ratings/me/summary");
        ownSummaryResponse.EnsureSuccessStatusCode();
        var ownSummary = await RatingTestClient.ReadSummaryAsync(ownSummaryResponse);
        Assert.Equal(scenario.Passenger.User.Id, ownSummary.UserId);
        Assert.Equal(5, ownSummary.AverageScore);
        Assert.Equal(1, ownSummary.RatingCount);
    }

    [Fact]
    public async Task UnknownDriverSummaryReturnsNotFound()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27821008001");

        var response = await client.GetAsync($"/api/v1/drivers/{Guid.NewGuid()}/ratings/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
