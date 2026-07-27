using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Authentication;
using Rydo.Application.Ratings;
using Rydo.Application.Trips;

namespace Rydo.Api.Tests;

internal static class RatingTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<(TokenPairResult Passenger, TokenPairResult Driver, TripResult Trip)> CompleteTripAsync(
        AuthenticationApiFactory factory,
        HttpClient client,
        string phoneSuffix,
        int identifier)
    {
        var passenger = await TripTestClient.CreatePassengerAsync(client, $"+278200{phoneSuffix}01");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(client, $"+278200{phoneSuffix}02", "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(factory, client, driver, identifier);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        await TripTestClient.TransitionAsync(client, trip.Id, "arrive");
        await TripTestClient.TransitionAsync(client, trip.Id, "start");
        var completed = await TripTestClient.TransitionAsync(client, trip.Id, "complete");
        return (passenger, driver, completed);
    }

    public static async Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid tripId, int score, string? comment) =>
        await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/ratings", new { score, comment });

    public static async Task<RatingResult> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<RatingResult>(JsonOptions))!;

    public static async Task<RatingSummaryResult> ReadSummaryAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<RatingSummaryResult>(JsonOptions))!;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
