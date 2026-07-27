using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Drivers;
using Rydo.Domain.Drivers;

namespace Rydo.Api.Tests;

public sealed class DriverProfileOnboardingTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task DriverCanCreateUpdateAndSubmitOwnProfile()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000401",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var missingResponse = await client.GetAsync("/api/v1/drivers/me/profile");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        var createResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/profile",
            new
            {
                firstName = "  Themba ",
                lastName = " Dlamini  ",
                email = " Themba.Dlamini@Example.COM ",
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await ReadProfileAsync(createResponse);

        Assert.Equal(tokens.User.Id, created.UserId);
        Assert.Equal("Themba", created.FirstName);
        Assert.Equal("Dlamini", created.LastName);
        Assert.Equal("themba.dlamini@example.com", created.Email);
        Assert.Equal(DriverOnboardingStatus.Draft, created.OnboardingStatus);
        Assert.True(created.CanEdit);
        Assert.Null(created.SubmittedAt);

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var updateResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/profile",
            new
            {
                firstName = "Themba",
                lastName = "Nkosi",
                email = (string?)null,
            });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await ReadProfileAsync(updateResponse);

        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
        Assert.Equal("Nkosi", updated.LastName);
        Assert.Null(updated.Email);

        await DriverDocumentTestClient.RegisterRequiredDocumentsAsync(client);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var submitResponse = await client.PostAsync(
            "/api/v1/drivers/me/onboarding/submit",
            null);
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await ReadProfileAsync(submitResponse);

        Assert.Equal(DriverOnboardingStatus.PendingReview, submitted.OnboardingStatus);
        Assert.False(submitted.CanEdit);
        Assert.NotNull(submitted.SubmittedAt);
        Assert.Equal(submitted.SubmittedAt, submitted.UpdatedAt);

        var getResponse = await client.GetAsync("/api/v1/drivers/me/profile");
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal(submitted, await ReadProfileAsync(getResponse));
    }

    [Fact]
    public async Task PendingReviewProfileCannotBeEditedOrResubmitted()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000402",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var createResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/profile",
            new { firstName = "Ayanda", lastName = "Khumalo", email = (string?)null });
        createResponse.EnsureSuccessStatusCode();
        await DriverDocumentTestClient.RegisterRequiredDocumentsAsync(client);
        var submitResponse = await client.PostAsync(
            "/api/v1/drivers/me/onboarding/submit",
            null);
        submitResponse.EnsureSuccessStatusCode();

        var editResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/profile",
            new { firstName = "Ayanda", lastName = "Zulu", email = (string?)null });
        var resubmitResponse = await client.PostAsync(
            "/api/v1/drivers/me/onboarding/submit",
            null);

        Assert.Equal(HttpStatusCode.Conflict, editResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, resubmitResponse.StatusCode);
    }

    [Fact]
    public async Task OnboardingSubmissionRequiresDriverProfile()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000403",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var response = await client.PostAsync("/api/v1/drivers/me/onboarding/submit", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OnboardingSubmissionRequiresAllCoreDocuments()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000405",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        await DriverDocumentTestClient.RegisterAsync(
            client,
            "IdentityDocument",
            "identity.pdf");

        var response = await client.PostAsync("/api/v1/drivers/me/onboarding/submit", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("DriversLicense", body, StringComparison.Ordinal);
        Assert.Contains("ProfessionalDrivingPermit", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PassengerCannotAccessDriverProfileEndpoints()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000404",
            "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var getResponse = await client.GetAsync("/api/v1/drivers/me/profile");
        var putResponse = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/profile",
            new { firstName = "Passenger", lastName = "Account", email = (string?)null });
        var submitResponse = await client.PostAsync(
            "/api/v1/drivers/me/onboarding/submit",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, submitResponse.StatusCode);
    }

    [Fact]
    public async Task DriverProfileRequiresAuthentication()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/drivers/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Dlamini", "themba@example.com")]
    [InlineData("Themba", "", "themba@example.com")]
    [InlineData("Themba", "Dlamini", "not-an-email")]
    public async Task InvalidDriverProfileIsRejected(
        string firstName,
        string lastName,
        string email)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            $"+2782000050{email.Length % 10}",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var response = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/profile",
            new { firstName, lastName, email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<DriverProfileResult> ReadProfileAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<DriverProfileResult>(JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
