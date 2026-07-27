using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Disputes;
using Rydo.Domain.Disputes;

namespace Rydo.Api.Tests;

internal static class DisputeTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static Task<HttpResponseMessage> OpenAsync(
        HttpClient client,
        Guid tripId,
        DisputeCategory category = DisputeCategory.Fare,
        string subject = "Incorrect fare",
        string description = "The final fare does not match the quoted amount.")
    {
        return client.PostAsJsonAsync(
            $"/api/v1/trips/{tripId}/disputes",
            new { category, subject, description });
    }

    public static Task<HttpResponseMessage> AddMessageAsync(
        HttpClient client,
        Guid disputeId,
        string body)
    {
        return client.PostAsJsonAsync(
            $"/api/v1/disputes/{disputeId}/messages",
            new { body });
    }

    public static async Task<DisputeDetailsResult> ReadAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<DisputeDetailsResult>(JsonOptions))!;
    }

    public static async Task<DisputeMessageResult> ReadMessageAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<DisputeMessageResult>(JsonOptions))!;
    }

    public static async Task<IReadOnlyList<DisputeSummaryResult>> ReadListAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<List<DisputeSummaryResult>>(
            JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
