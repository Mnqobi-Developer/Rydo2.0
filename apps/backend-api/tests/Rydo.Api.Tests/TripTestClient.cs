using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Authentication;
using Rydo.Application.Trips;
using Rydo.Application.Pricing;
using Rydo.Domain.Pricing;

namespace Rydo.Api.Tests;

internal static class TripTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<TokenPairResult> CreatePassengerAsync(
        HttpClient client,
        string phoneNumber)
    {
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            phoneNumber,
            "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        var profileResponse = await client.PutAsJsonAsync(
            "/api/v1/passengers/me/profile",
            new { firstName = "Nandi", lastName = "Mokoena", email = (string?)null });
        profileResponse.EnsureSuccessStatusCode();
        return tokens;
    }

    public static async Task<TripResult> RequestAsync(HttpClient client)
    {
        var quoteResponse = await client.PostAsJsonAsync(
            "/api/v1/pricing/quotes",
            ValidQuoteRequest());
        quoteResponse.EnsureSuccessStatusCode();
        var quote = (await quoteResponse.Content.ReadFromJsonAsync<FareQuoteResult>(JsonOptions))!;
        var response = await client.PostAsJsonAsync(
            "/api/v1/trips",
            ValidRequest(quote.Id));
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response);
    }

    public static async Task<TripResult> TransitionAsync(
        HttpClient client,
        Guid tripId,
        string transition)
    {
        var response = await client.PostAsync(
            $"/api/v1/trips/{tripId}/{transition}",
            null);
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response);
    }

    public static async Task<TripResult> ReadAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<TripResult>(JsonOptions))!;
    }

    public static async Task<IReadOnlyList<TripResult>> ReadListAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<List<TripResult>>(JsonOptions))!;
    }

    public static object ValidRequest(Guid? quoteId = null, RideCategory category = RideCategory.Solo)
    {
        return new
        {
            pickupAddress = "12 Long Street, Cape Town",
            pickupLatitude = -33.9249,
            pickupLongitude = 18.4241,
            destinationAddress = "V&A Waterfront, Cape Town",
            destinationLatitude = -33.9036,
            destinationLongitude = 18.4209,
            fareQuoteId = quoteId ?? Guid.NewGuid(),
            rideCategory = category,
        };
    }

    public static object ValidQuoteRequest() => new
    {
        pickup = new { latitude = -33.9249, longitude = 18.4241 },
        destination = new { latitude = -33.9036, longitude = 18.4209 },
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
