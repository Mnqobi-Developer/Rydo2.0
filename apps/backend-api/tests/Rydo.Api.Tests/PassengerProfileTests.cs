using System.Net;
using System.Net.Http.Json;
using Rydo.Application.Passengers;

namespace Rydo.Api.Tests;

public sealed class PassengerProfileTests
{
    [Fact]
    public async Task PassengerCanCreateReadAndUpdateOwnProfile()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000201",
            "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var missingResponse = await client.GetAsync("/api/v1/passengers/me/profile");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        var createResponse = await client.PutAsJsonAsync(
            "/api/v1/passengers/me/profile",
            new
            {
                firstName = "  Nandi ",
                lastName = " Mokoena  ",
                email = " Nandi.Mokoena@Example.COM ",
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<PassengerProfileResult>();

        Assert.NotNull(created);
        Assert.Equal(tokens.User.Id, created.UserId);
        Assert.Equal("Nandi", created.FirstName);
        Assert.Equal("Mokoena", created.LastName);
        Assert.Equal("nandi.mokoena@example.com", created.Email);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var updateResponse = await client.PutAsJsonAsync(
            "/api/v1/passengers/me/profile",
            new
            {
                firstName = "Nandi",
                lastName = "Nkosi",
                email = (string?)null,
            });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<PassengerProfileResult>();

        Assert.NotNull(updated);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
        Assert.Equal("Nkosi", updated.LastName);
        Assert.Null(updated.Email);

        var getResponse = await client.GetAsync("/api/v1/passengers/me/profile");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<PassengerProfileResult>();
        Assert.Equal(updated, fetched);
    }

    [Fact]
    public async Task DriverCannotAccessPassengerProfileEndpoint()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000202",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var getResponse = await client.GetAsync("/api/v1/passengers/me/profile");
        var putResponse = await client.PutAsJsonAsync(
            "/api/v1/passengers/me/profile",
            new { firstName = "Driver", lastName = "Account", email = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
    }

    [Fact]
    public async Task PassengerProfileRequiresAuthentication()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/passengers/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Mokoena", "nandi@example.com")]
    [InlineData("Nandi", "", "nandi@example.com")]
    [InlineData("Nandi", "Mokoena", "not-an-email")]
    public async Task InvalidPassengerProfileIsRejected(
        string firstName,
        string lastName,
        string email)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            $"+2782000030{email.Length % 10}",
            "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var response = await client.PutAsJsonAsync(
            "/api/v1/passengers/me/profile",
            new { firstName, lastName, email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
