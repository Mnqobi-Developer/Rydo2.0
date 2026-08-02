using System.Net.Http.Json;
using System.Net.Http.Headers;
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
        using var form = CreateUploadForm(
            documentType,
            originalFileName,
            "application/pdf",
            CreatePdfBytes(hashCharacter, 1024));
        var response = await client.PostAsync("/api/v1/drivers/me/documents", form);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<DriverDocumentResult>(JsonOptions))!;
    }

    public static MultipartFormDataContent CreateUploadForm(
        string documentType,
        string originalFileName,
        string contentType,
        byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(documentType), "documentType");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", originalFileName);
        return form;
    }

    public static byte[] CreatePdfBytes(char fillCharacter, int length)
    {
        var bytes = Enumerable.Repeat((byte)fillCharacter, length).ToArray();
        "%PDF-"u8.CopyTo(bytes);
        return bytes;
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
