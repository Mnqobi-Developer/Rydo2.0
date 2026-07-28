using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Admin;
using Rydo.Application.Authentication;
using Rydo.Domain.Disputes;

namespace Rydo.Api.Tests;

internal static class AdminTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<TokenPairResult> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/auth/login",
            new
            {
                email = "admin@rydo.test",
                password = "test-only-admin-password",
            });
        response.EnsureSuccessStatusCode();
        return await AuthenticationTestClient.ReadTokenPairAsync(response);
    }

    public static Task<HttpResponseMessage> ReviewDriverAsync(
        HttpClient client,
        Guid driverUserId,
        bool approve,
        string? reason = null)
    {
        return client.PostAsJsonAsync(
            $"/api/v1/admin/drivers/{driverUserId}/review",
            new { approve, reason });
    }

    public static Task<HttpResponseMessage> ReviewDisputeAsync(
        HttpClient client,
        Guid disputeId,
        DisputeStatus status,
        string? resolution = null)
    {
        return client.PostAsJsonAsync(
            $"/api/v1/admin/disputes/{disputeId}/review",
            new { status, resolution });
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
