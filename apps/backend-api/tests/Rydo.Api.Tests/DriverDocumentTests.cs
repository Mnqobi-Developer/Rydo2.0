using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rydo.Domain.Drivers;
using Rydo.Infrastructure.Persistence;

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

        var bytes = DriverDocumentTestClient.CreatePdfBytes('A', 2048);
        using var upload = DriverDocumentTestClient.CreateUploadForm(
            "IdentityDocument",
            "identity-document.pdf",
            "application/pdf",
            bytes);
        var registerResponse = await client.PostAsync(
            "/api/v1/drivers/me/documents",
            upload);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var responseBody = await registerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageObjectKey", responseBody, StringComparison.OrdinalIgnoreCase);
        var registered = await DriverDocumentTestClient.ReadDocumentAsync(registerResponse);
        Assert.Equal(DriverDocumentType.IdentityDocument, registered.DocumentType);
        Assert.Equal("identity-document.pdf", registered.OriginalFileName);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), registered.Sha256);
        Assert.Equal(DriverDocumentReviewStatus.PendingReview, registered.ReviewStatus);

        var getResponse = await client.GetAsync($"/api/v1/drivers/me/documents/{registered.Id}");
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal(registered, await DriverDocumentTestClient.ReadDocumentAsync(getResponse));

        var contentResponse = await client.GetAsync(
            $"/api/v1/drivers/me/documents/{registered.Id}/content");
        contentResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", contentResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await contentResponse.Content.ReadAsByteArrayAsync());

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

        using var upload = DriverDocumentTestClient.CreateUploadForm(
            "DriversLicense",
            "replacement.pdf",
            "application/pdf",
            DriverDocumentTestClient.CreatePdfBytes('B', 32));
        var response = await client.PostAsync("/api/v1/drivers/me/documents", upload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RejectedDocumentCanBeReplacedWithANewProtectedUpload()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000612",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        var rejected = await DriverDocumentTestClient.RegisterAsync(
            client,
            "DriversLicense",
            "old-license.pdf");

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
            var entity = await database.DriverDocuments.SingleAsync(item => item.Id == rejected.Id);
            entity.Reject("The licence image is unreadable.", factory.Clock.GetUtcNow());
            await database.SaveChangesAsync();
        }

        var replacement = await DriverDocumentTestClient.RegisterAsync(
            client,
            "DriversLicense",
            "new-license.pdf",
            'Z');
        var listResponse = await client.GetAsync("/api/v1/drivers/me/documents");
        listResponse.EnsureSuccessStatusCode();
        var current = await DriverDocumentTestClient.ReadDocumentsAsync(listResponse);

        Assert.NotEqual(rejected.Id, replacement.Id);
        Assert.Equal(DriverDocumentReviewStatus.PendingReview, replacement.ReviewStatus);
        Assert.Equal([replacement], current);
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

        using var upload = DriverDocumentTestClient.CreateUploadForm(
            "IdentityDocument",
            "replacement.pdf",
            "application/pdf",
            DriverDocumentTestClient.CreatePdfBytes('B', 32));
        var response = await client.PostAsync("/api/v1/drivers/me/documents", upload);

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

        using var upload = DriverDocumentTestClient.CreateUploadForm(
            "IdentityDocument",
            "identity.pdf",
            "application/pdf",
            DriverDocumentTestClient.CreatePdfBytes('A', 16));
        var response = await client.PostAsync("/api/v1/drivers/me/documents", upload);

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
        var contentResponse = await client.GetAsync(
            $"/api/v1/drivers/me/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, contentResponse.StatusCode);
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
        using var upload = DriverDocumentTestClient.CreateUploadForm(
            "IdentityDocument",
            "identity.pdf",
            "application/pdf",
            DriverDocumentTestClient.CreatePdfBytes('A', 16));
        var registerResponse = await client.PostAsync("/api/v1/drivers/me/documents", upload);

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, registerResponse.StatusCode);
    }

    [Theory]
    [InlineData("IdentityDocument", "identity.exe", "application/octet-stream", 1)]
    [InlineData("IdentityDocument", "identity.pdf", "application/pdf", 0)]
    [InlineData("Unknown", "identity.pdf", "application/pdf", 1)]
    public async Task InvalidDocumentUploadIsRejected(
        string documentType,
        string originalFileName,
        string contentType,
        int sizeBytes)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            $"+2782000070{sizeBytes % 10}",
            "Driver");
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);

        using var upload = DriverDocumentTestClient.CreateUploadForm(
            documentType,
            originalFileName,
            contentType,
            new byte[sizeBytes]);
        var response = await client.PostAsync("/api/v1/drivers/me/documents", upload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

}
