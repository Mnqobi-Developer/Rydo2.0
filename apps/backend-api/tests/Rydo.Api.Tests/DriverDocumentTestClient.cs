using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Drivers;

namespace Rydo.Api.Tests;

internal static class DriverDocumentTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task RegisterRequiredDocumentsAsync(HttpClient client)
    {
        await RegisterAsync(client, "IdentityDocument", "identity.pdf", 'A');
        await RegisterAsync(client, "DriversLicense", "license.pdf", 'B');
        await RegisterAsync(client, "ProfessionalDrivingPermit", "prdp.pdf", 'C');
    }

    public static async Task<DriverDocumentResult> RegisterAsync(
        HttpClient client,
        string documentType,
        string originalFileName,
        char hashCharacter = 'A')
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/drivers/me/documents",
            new
            {
                documentType,
                originalFileName,
                contentType = "application/pdf",
                sizeBytes = 1024,
                sha256 = new string(hashCharacter, 64),
            });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<DriverDocumentResult>(JsonOptions))!;
    }

    public static async Task CreateProfileAsync(HttpClient client)
    {
        var response = await client.PutAsJsonAsync(
            "/api/v1/drivers/me/profile",
            new { firstName = "Test", lastName = "Driver", email = (string?)null });
        response.EnsureSuccessStatusCode();
    }

    public static async Task<DriverDocumentResult> ReadDocumentAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<DriverDocumentResult>(JsonOptions))!;
    }

    public static async Task<IReadOnlyList<DriverDocumentResult>> ReadDocumentsAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<List<DriverDocumentResult>>(JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
