using System.Net;
using System.Net.Http.Json;
using Rydo.Domain.Drivers;

namespace Rydo.Api.Tests;

public sealed class DriverDocumentTests
{
    [Fact]
    public async Task DriverCanRegisterListAndReadOwnedDocumentMetadata()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000601",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);

        var emptyResponse = await client.GetAsync("/api/v1/drivers/me/documents");
        emptyResponse.EnsureSuccessStatusCode();
        Assert.Empty(await DriverDocumentTestClient.ReadDocumentsAsync(emptyResponse));

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/documents",
            new
            {
                documentType = "IdentityDocument",
                originalFileName = " identity-document.pdf ",
                contentType = "application/pdf",
                sizeBytes = 2048,
                sha256 = new string('a', 64),
            });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var responseBody = await registerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageObjectKey", responseBody, StringComparison.OrdinalIgnoreCase);
        var registered = await DriverDocumentTestClient.ReadDocumentAsync(registerResponse);
        Assert.Equal(DriverDocumentType.IdentityDocument, registered.DocumentType);
        Assert.Equal("identity-document.pdf", registered.OriginalFileName);
        Assert.Equal(new string('A', 64), registered.Sha256);
        Assert.Equal(DriverDocumentReviewStatus.PendingReview, registered.ReviewStatus);

        var getResponse = await client.GetAsync($"/api/v1/drivers/me/documents/{registered.Id}");
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal(registered, await DriverDocumentTestClient.ReadDocumentAsync(getResponse));

        var listResponse = await client.GetAsync("/api/v1/drivers/me/documents");
        listResponse.EnsureSuccessStatusCode();
        Assert.Equal([registered], await DriverDocumentTestClient.ReadDocumentsAsync(listResponse));
    }

    [Fact]
    public async Task DuplicateCurrentDocumentTypeIsRejected()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000602",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        await DriverDocumentTestClient.RegisterAsync(
            client,
            "DriversLicense",
            "license.pdf");

        var response = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/documents",
            new
            {
                documentType = "DriversLicense",
                originalFileName = "replacement.pdf",
                contentType = "application/pdf",
                sizeBytes = 1024,
                sha256 = new string('B', 64),
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DocumentsCannotChangeWhileOnboardingIsPendingReview()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000607",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        await DriverDocumentTestClient.RegisterRequiredDocumentsAsync(client);
        await DriverVehicleTestClient.UpsertAsync(client);
        var submitResponse = await client.PostAsync(
            "/api/v1/drivers/me/onboarding/submit",
            null);
        submitResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/documents",
            new
            {
                documentType = "ProofOfAddress",
                originalFileName = "proof-of-address.pdf",
                contentType = "application/pdf",
                sizeBytes = 1024,
                sha256 = new string('D', 64),
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DriverDocumentRegistrationRequiresProfile()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000603",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/documents",
            ValidDocumentRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DriverCannotReadAnotherDriversDocument()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var ownerTokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000604",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, ownerTokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        var document = await DriverDocumentTestClient.RegisterAsync(
            client,
            "IdentityDocument",
            "identity.pdf");

        var otherTokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000605",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, otherTokens.AccessToken);

        var response = await client.GetAsync($"/api/v1/drivers/me/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PassengerCannotAccessDriverDocuments()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000606",
            "Passenger");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/drivers/me/documents");
        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/documents",
            ValidDocumentRequest());

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, registerResponse.StatusCode);
    }

    [Theory]
    [InlineData("IdentityDocument", "../identity.pdf", "application/pdf", 1024, 64)]
    [InlineData("IdentityDocument", "identity.exe", "application/octet-stream", 1024, 64)]
    [InlineData("IdentityDocument", "identity.pdf", "application/pdf", 0, 64)]
    [InlineData("IdentityDocument", "identity.pdf", "application/pdf", 10485761, 64)]
    [InlineData("IdentityDocument", "identity.pdf", "application/pdf", 1024, 63)]
    [InlineData("Unknown", "identity.pdf", "application/pdf", 1024, 64)]
    public async Task InvalidDocumentMetadataIsRejected(
        string documentType,
        string originalFileName,
        string contentType,
        long sizeBytes,
        int hashLength)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            $"+2782000070{hashLength % 10}",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/documents",
            new
            {
                documentType,
                originalFileName,
                contentType,
                sizeBytes,
                sha256 = new string('A', hashLength),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object ValidDocumentRequest()
    {
        return new
        {
            documentType = "IdentityDocument",
            originalFileName = "identity.pdf",
            contentType = "application/pdf",
            sizeBytes = 1024,
            sha256 = new string('A', 64),
        };
    }
}
