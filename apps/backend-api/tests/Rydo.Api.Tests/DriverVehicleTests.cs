using System.Net;
using System.Net.Http.Json;
using Rydo.Domain.Drivers;

namespace Rydo.Api.Tests;

public sealed class DriverVehicleTests
{
    [Fact]
    public async Task DriverCanCreateReadAndUpdateOwnVehicle()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000801",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);

        var missingResponse = await client.GetAsync("/api/v1/drivers/me/vehicle");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        var createResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            new
            {
                make = " Toyota ",
                model = " Corolla ",
                year = 2024,
                color = " White ",
                registrationNumber = " ca 123-456 ",
                vehicleIdentificationNumber = "1hgcm82633a004352",
                seatCapacity = 4,
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await DriverVehicleTestClient.ReadAsync(createResponse);

        Assert.Equal(tokens.User.Id, created.DriverUserId);
        Assert.Equal("Toyota", created.Make);
        Assert.Equal("Corolla", created.Model);
        Assert.Equal("White", created.Color);
        Assert.Equal("CA 123-456", created.RegistrationNumber);
        Assert.Equal("1HGCM82633A004352", created.VehicleIdentificationNumber);
        Assert.Equal(DriverVehicleReviewStatus.PendingReview, created.ReviewStatus);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var updateResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            new
            {
                make = "Toyota",
                model = "Corolla Quest",
                year = 2025,
                color = "Blue",
                registrationNumber = "CA 654-321",
                vehicleIdentificationNumber = "1HGCM82633A004352",
                seatCapacity = 4,
            });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await DriverVehicleTestClient.ReadAsync(updateResponse);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
        Assert.Equal("Corolla Quest", updated.Model);
        Assert.Equal("CA 654-321", updated.RegistrationNumber);

        var getResponse = await client.GetAsync("/api/v1/drivers/me/vehicle");
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal(updated, await DriverVehicleTestClient.ReadAsync(getResponse));
    }

    [Fact]
    public async Task VehicleRegistrationRequiresDriverProfile()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000802",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var response = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            ValidVehicleRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationAndVinCannotBeSharedAcrossDrivers()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var firstTokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000803",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, firstTokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        await DriverVehicleTestClient.UpsertAsync(client);

        var secondTokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000804",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, secondTokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);

        var response = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            ValidVehicleRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task VehicleCannotChangeWhileOnboardingIsPendingReview()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000805",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        await DriverDocumentTestClient.RegisterRequiredDocumentsAsync(client);
        await DriverVehicleTestClient.UpsertAsync(client);
        var submitResponse = await client.PostAsync(
            "/api/v1/drivers/me/onboarding/submit",
            null);
        submitResponse.EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            new
            {
                make = "Toyota",
                model = "Corolla Quest",
                year = 2025,
                color = "Blue",
                registrationNumber = "CA 654-321",
                vehicleIdentificationNumber = "1HGCM82633A004352",
                seatCapacity = 4,
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PassengerCannotAccessDriverVehicle()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000806",
            "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var getResponse = await client.GetAsync("/api/v1/drivers/me/vehicle");
        var putResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            ValidVehicleRequest());

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
    }

    [Theory]
    [InlineData("", "Corolla", 2024, "White", "CA 123-456", "1HGCM82633A004352", 4)]
    [InlineData("Toyota", "", 2024, "White", "CA 123-456", "1HGCM82633A004352", 4)]
    [InlineData("Toyota", "Corolla", 1979, "White", "CA 123-456", "1HGCM82633A004352", 4)]
    [InlineData("Toyota", "Corolla", 2024, "White", "!", "1HGCM82633A004352", 4)]
    [InlineData("Toyota", "Corolla", 2024, "White", "CA 123-456", "INVALIDVIN", 4)]
    [InlineData("Toyota", "Corolla", 2024, "White", "CA 123-456", "1HGCM82633A004352", 0)]
    [InlineData("Toyota", "Corolla", 2099, "White", "CA 123-456", "1HGCM82633A004352", 4)]
    public async Task InvalidVehicleInformationIsRejected(
        string make,
        string model,
        int year,
        string color,
        string registrationNumber,
        string vehicleIdentificationNumber,
        int seatCapacity)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            $"+2782000090{year % 10}",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);

        var response = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/vehicle",
            new
            {
                make,
                model,
                year,
                color,
                registrationNumber,
                vehicleIdentificationNumber,
                seatCapacity,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object ValidVehicleRequest()
    {
        return new
        {
            make = "Toyota",
            model = "Corolla",
            year = 2024,
            color = "White",
            registrationNumber = "CA 123-456",
            vehicleIdentificationNumber = "1HGCM82633A004352",
            seatCapacity = 4,
        };
    }
}
