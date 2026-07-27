using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Drivers;

namespace Rydo.Api.Tests;

internal static class DriverVehicleTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<DriverVehicleResult> UpsertAsync(
        HttpClient client,
        string registrationNumber = "CA 123-456",
        string vehicleIdentificationNumber = "1HGCM82633A004352")
    {
        var response = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            new
            {
                make = "Toyota",
                model = "Corolla",
                year = 2024,
                color = "White",
                registrationNumber,
                vehicleIdentificationNumber,
                seatCapacity = 4,
            });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<DriverVehicleResult>(JsonOptions))!;
    }

    public static async Task<DriverVehicleResult> ReadAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<DriverVehicleResult>(JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
