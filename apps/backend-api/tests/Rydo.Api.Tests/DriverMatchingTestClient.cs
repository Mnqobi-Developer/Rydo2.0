using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rydo.Application.Authentication;
using Rydo.Application.Matching;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Api.Tests;

internal static class DriverMatchingTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task MakeEligibleAndOnlineAsync(
        AuthenticationApiFactory factory,
        HttpClient client,
        TokenPairResult driver,
        int identifier,
        double latitude = -33.925,
        double longitude = 18.424)
    {
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        await DriverDocumentTestClient.RegisterRequiredDocumentsAsync(client);
        await DriverVehicleTestClient.UpsertAsync(
            client,
            $"CA M{identifier:D4}",
            $"1HGCM82633A00{identifier:D4}");
        var submitResponse = await client.PostAsync(
            "/api/v1/drivers/me/onboarding/submit",
            null);
        submitResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
            var profile = await database.DriverProfiles.SingleAsync(
                item => item.UserId == driver.User.Id);
            profile.Approve(factory.Clock.GetUtcNow());
            await database.SaveChangesAsync();
        }

        var onlineResponse = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/availability/online",
            new { latitude, longitude });
        onlineResponse.EnsureSuccessStatusCode();
    }

    public static async Task<TripMatchingResult> MatchAsync(
        HttpClient client,
        string passengerAccessToken,
        Guid tripId)
    {
        AuthenticationTestClient.UseBearerToken(client, passengerAccessToken);
        var response = await client.PostAsync($"/api/v1/trips/{tripId}/matching", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripMatchingResult>(JsonOptions))!;
    }

    public static async Task<IReadOnlyList<TripOfferResult>> ReadOffersAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<List<TripOfferResult>>(JsonOptions))!;
    }

    public static async Task<DriverAvailabilityResult> ReadAvailabilityAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<DriverAvailabilityResult>(JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
